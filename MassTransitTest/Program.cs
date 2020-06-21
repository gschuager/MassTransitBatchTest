using GreenPipes;
using MassTransit;
using MassTransit.Context;
using MassTransit.Definition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GreenPipes.Util;

namespace MassTransitTest
{
    class Program
    {
        public const int MessagesCount = 10000;

        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("MassTransit", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            var provider = ConfigureServiceProvider();
            LogContext.ConfigureCurrentLogContext(provider.GetRequiredService<ILoggerFactory>());

            var logger = provider.GetRequiredService<ILogger<Program>>();
            var bus = provider.GetRequiredService<IBusControl>();
            var counter1 = provider.GetRequiredService<MessageCounter<DoWork>>();
            var counter2 = provider.GetRequiredService<MessageCounter<WorkDone>>();

            try
            {
                await bus.StartAsync();

                var messages = Enumerable.Range(0, MessagesCount).Select(_ => new DoWork());
                await bus.SendConcurrently(messages, 100);
                // foreach (var msg in messages)
                // {
                //     await bus.Send(msg);
                // }

                await Task.WhenAll(counter1.Completed, counter2.Completed);

                logger.LogInformation("DoWork messages rate {0} msg/s - concurrent batch consumers {1}", await counter1.GetRate(), ConcurrencyCounter<DoWorkConsumer>.MaxConsumerCount);
                logger.LogInformation("WorkDone messages rate {0} msg/s - concurrent batch consumers {1}", await counter2.GetRate(), ConcurrencyCounter<WorkDoneConsumer>.MaxConsumerCount);
            }
            finally
            {
                await bus.StopAsync();
            }
        }

        private static ServiceProvider ConfigureServiceProvider()
        {
            var services = new ServiceCollection();

            services.AddSingleton(typeof(MessageCounter<>));

            services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddSerilog());

            EndpointConvention.Map<DoWork>(new Uri("queue:do-work"));

            services.AddMassTransit(x =>
            {
                x.SetEndpointNameFormatter(KebabCaseEndpointNameFormatter.Instance);

                x.AddConsumers(Assembly.GetExecutingAssembly());

                x.AddBus(context => Bus.Factory.CreateUsingRabbitMq(cfg =>
                {
                    cfg.Host("localhost");

                    cfg.UseInMemoryOutbox();

                    cfg.ReceiveEndpoint("do-work", e =>
                    {
                        e.PrefetchCount = 5000;
                        e.PurgeOnStartup = true;
                        e.Batch<DoWork>(b =>
                        {
                            b.MessageLimit = 100;
                            b.TimeLimit = TimeSpan.FromMilliseconds(50);

                            b.Consumer<DoWorkConsumer, DoWork>(context);
                        });
                    });

                    cfg.ReceiveEndpoint("work-done", e =>
                    {
                        e.PrefetchCount = 5000;
                        e.PurgeOnStartup = true;
                        e.Batch<WorkDone>(b =>
                        {
                            b.MessageLimit = 100;
                            b.TimeLimit = TimeSpan.FromMilliseconds(50);

                            b.Consumer<WorkDoneConsumer, WorkDone>(context);
                        });
                    });
                }));
            });

            var provider = services.BuildServiceProvider();
            return provider;
        }
    }

    public class MessageCounter<T>
    {
        private readonly int messageCount;
        private readonly TaskCompletionSource<TimeSpan> completed = TaskUtil.GetTask<TimeSpan>();
        private readonly Stopwatch stopwatch;

        private int consumed;

        public MessageCounter()
        {
            messageCount = Program.MessagesCount;
            stopwatch = Stopwatch.StartNew();
        }

        public Task<TimeSpan> Completed => completed.Task;

        public void Consumed(int n)
        {
            var c = Interlocked.Add(ref consumed, n);

            if (c == messageCount)
            {
                completed.TrySetResult(stopwatch.Elapsed);
            }
        }

        public async Task<double> GetRate() => consumed / (await Completed).TotalSeconds;
    }

    public class DoWork
    {
    }

    public class DoWorkConsumer : IConsumer<Batch<DoWork>>
    {
        private readonly ILogger<DoWorkConsumer> logger;
        private readonly MessageCounter<DoWork> counter;

        public DoWorkConsumer(ILogger<DoWorkConsumer> logger, MessageCounter<DoWork> counter)
        {
            this.logger = logger;
            this.counter = counter;
        }

        public async Task Consume(ConsumeContext<Batch<DoWork>> context)
        {
            using var _ = ConcurrencyCounter<DoWorkConsumer>.Measure();
            
            if (DuplicatesDetector<DoWork>.AlreadyReceived(logger, context.Message))
            {
                return;
            }

            foreach (var msg in context.Message)
            {
                await context.Publish(new WorkDone());
            }

            // var actions = context.Message.Select(msg => (Func<Task>) (() => context.Publish(new WorkDone())));
            // await actions.ExecuteSequentially();

            // logger.LogInformation("Consumed {0} DoWork", context.Message.Length);

            // await Task.Delay(50);

            counter.Consumed(context.Message.Length);
        }
    }

    public class WorkDone
    {
    }

    public class WorkDoneConsumer : IConsumer<Batch<WorkDone>>
    {
        private readonly ILogger<WorkDoneConsumer> logger;
        private readonly MessageCounter<WorkDone> counter;

        public WorkDoneConsumer(ILogger<WorkDoneConsumer> logger, MessageCounter<WorkDone> counter)
        {
            this.logger = logger;
            this.counter = counter;
        }

        public async Task Consume(ConsumeContext<Batch<WorkDone>> context)
        {
            using var _ = ConcurrencyCounter<WorkDoneConsumer>.Measure();

            if (DuplicatesDetector<WorkDone>.AlreadyReceived(logger, context.Message))
            {
                return;
            }

            // logger.LogInformation("Consumed {0} WorkDone", context.Message.Length);

            // await Task.Delay(50);

            counter.Consumed(context.Message.Length);
        }
    }
}