using MassTransit;
using MassTransit.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MassTransitTest
{
    class Program
    {
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

            var bus = provider.GetRequiredService<IBusControl>();

            try
            {
                await bus.StartAsync();

                var messages = Enumerable.Range(0, 15000).Select(x => new DoWork());
                foreach (var msg in messages)
                {
                    await bus.Send(msg);
                }

                Console.ReadKey();
            }
            finally
            {
                await bus.StopAsync();
            }
        }

        private static ServiceProvider ConfigureServiceProvider()
        {
            var services = new ServiceCollection();

            services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddSerilog());

            EndpointConvention.Map<DoWork>(new Uri("queue:test-queue"));

            services.AddMassTransit(x =>
            {
                x.AddConsumers(Assembly.GetExecutingAssembly());

                x.AddBus(provider => Bus.Factory.CreateUsingInMemory(cfg =>
                {
                    cfg.ReceiveEndpoint("test-queue", e =>
                    {
                        e.Batch<DoWork>(b =>
                        {
                            b.MessageLimit = 100;
                            b.TimeLimit = TimeSpan.FromMilliseconds(50);

                            b.Consumer<DoWorkConsumer, DoWork>(provider);
                        });
                    });
                }));
            });

            var provider = services.BuildServiceProvider();
            return provider;
        }
    }

    public class DoWork
    {
    }

    public class DoWorkConsumer : IConsumer<Batch<DoWork>>
    {
        private readonly ILogger<DoWorkConsumer> logger;

        private static readonly HashSet<Guid?> alreadyReceivedMessages = new HashSet<Guid?>();

        public DoWorkConsumer(ILogger<DoWorkConsumer> logger)
        {
            this.logger = logger;
        }

        public async Task Consume(ConsumeContext<Batch<DoWork>> context)
        {
            lock (alreadyReceivedMessages)
            {
                foreach (var msg in context.Message)
                {
                    if (alreadyReceivedMessages.Contains(msg.MessageId))
                    {
                        logger.LogError("DUPLICATED MESSAGE");
                        return;
                    }
                    else
                    {
                        alreadyReceivedMessages.Add(msg.MessageId);
                    }
                }
            }

            for (int i = 0; i < 50000000; i++)
            {
                if (i % 5000000 == 0)
                {
                    await Task.Yield();
                }
            }
        }
    }
}
