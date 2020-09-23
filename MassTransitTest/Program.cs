﻿using GreenPipes;
using MassTransit;
using MassTransit.Context;
using MassTransit.Definition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MassTransit.ConsumeConfigurators;
using MassTransit.RabbitMqTransport;

namespace MassTransitTest
{
    class Program
    {
        public const int MessagesCount = 100000;

        static async Task Main(string[] args)
        {
            File.Delete("batch.log");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                // .MinimumLevel.Override("MassTransit", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {messageId} {Message:lj}{NewLine}{Exception}")
                .WriteTo.File("batch.log",
                    outputTemplate:
                    "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {messageId} {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            var provider = ConfigureServiceProvider();
            LogContext.ConfigureCurrentLogContext(provider.GetRequiredService<ILoggerFactory>());

            var bus = provider.GetRequiredService<IBusControl>();

            try
            {
                await bus.StartAsync();

                var messages = Enumerable.Range(0, MessagesCount).Select(_ => new DoWork());
                await bus.SendConcurrently(messages);

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

            services.AddSingleton(typeof(MessageCounter2));

            services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddSerilog());

            EndpointConvention.Map<DoWork>(new Uri("queue:do-work"));

            services.AddMassTransit(cfg =>
            {
                cfg.SetEndpointNameFormatter(KebabCaseEndpointNameFormatter.Instance);

                cfg.AddConsumers(Assembly.GetExecutingAssembly());

                cfg.UsingRabbitMq(ConfigureBus);
            });

            var provider = services.BuildServiceProvider();
            return provider;
        }

        private static void ConfigureBus(IBusRegistrationContext context, IRabbitMqBusFactoryConfigurator cfg)
        {
            cfg.Host("localhost");

            cfg.PrefetchCount = 500;

            cfg.UseMessageRetry(r => { r.Immediate(2); });
            cfg.UseLogging();
            cfg.UseInMemoryOutbox(x => x.ConcurrentMessageDelivery = true);

            cfg.ConfigureEndpoints(context);
        }
    }

    public class DoWork
    {
    }
    
    public class WorkDone
    {
        public Guid TaskId { get; set; }
    }

    public class DoWorkConsumerDefinition : ConsumerDefinition<DoWorkConsumer>
    {
        protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<DoWorkConsumer> consumerConfigurator)
        {
            ((IRabbitMqReceiveEndpointConfigurator) endpointConfigurator).PrefetchCount = 5000;
            consumerConfigurator.Options<BatchOptions>(b =>
            {
                b.MessageLimit = 100;
                b.TimeLimit = TimeSpan.FromMilliseconds(100);
                b.ConcurrencyLimit = 10;
            });
        }
    }

    public class DoWorkConsumer : IConsumer<Batch<DoWork>>
    {
        private static int n;

        private readonly ILogger<DoWorkConsumer> logger;
        private readonly MessageCounter2 counter;

        public DoWorkConsumer(ILogger<DoWorkConsumer> logger, MessageCounter2 counter)
        {
            this.logger = logger;
            this.counter = counter;
        }

        public async Task Consume(ConsumeContext<Batch<DoWork>> context)
        {
            foreach (var msg in context.Message)
            {
                await context.Publish(new WorkDone { TaskId = msg.MessageId!.Value });
            }

            counter.Consumed(nameof(DoWork), context.Message.Select(x => x.MessageId!.Value).ToArray());
        }
    }
    
    
    public class WorkDoneConsumerDefinition : ConsumerDefinition<WorkDoneConsumer>
    {
        protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<WorkDoneConsumer> consumerConfigurator)
        {
            ((IRabbitMqReceiveEndpointConfigurator) endpointConfigurator).PrefetchCount = 5000;
            consumerConfigurator.Options<BatchOptions>(b =>
            {
                b.MessageLimit = 100;
                b.TimeLimit = TimeSpan.FromMilliseconds(100);
                b.ConcurrencyLimit = 10;
            });
        }
    }

    public class WorkDoneConsumer : IConsumer<Batch<WorkDone>>
    {
        private static int n;

        private readonly ILogger<WorkDoneConsumer> logger;
        private readonly MessageCounter2 counter;

        public WorkDoneConsumer(ILogger<WorkDoneConsumer> logger, MessageCounter2 counter)
        {
            this.logger = logger;
            this.counter = counter;
        }

        public async Task Consume(ConsumeContext<Batch<WorkDone>> context)
        {
            counter.Consumed(nameof(WorkDone), context.Message.Select(x => x.Message.TaskId).ToArray());
        }
    }
}