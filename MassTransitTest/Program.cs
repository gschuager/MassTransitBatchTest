using GreenPipes;
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
        public const int MessagesCount = 2;

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
            
            var result = bus.GetProbeResult();
            await File.WriteAllTextAsync("bus.json", result.ToJsonString());

            try
            {
                await bus.StartAsync();

                var messages = Enumerable.Range(0, MessagesCount).Select(_ => new DoWork());
                await bus.SendConcurrently(messages, 100);

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

            services.AddSingleton(typeof(MessageCounter<>));

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
            cfg.Host("localhost", "/", x =>
            {
                x.Username("guest");
                x.Password("guest");
            });

            cfg.PrefetchCount = 10;

            cfg.UseMessageRetry(r => { r.Immediate(1); });
            cfg.UseLogging();
            
            cfg.ConfigureEndpoints(context);
        }
    }

    public class DoWork
    {
    }

    public class DoWorkConsumerDefinition : ConsumerDefinition<DoWorkConsumer>
    {
        protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<DoWorkConsumer> consumerConfigurator)
        {
            ((IRabbitMqReceiveEndpointConfigurator) endpointConfigurator).PrefetchCount = 100;
            consumerConfigurator.Options<BatchOptions>(b =>
            {
                b.MessageLimit = 2;
                // b.TimeLimit = TimeSpan.FromMilliseconds(100);
                // b.ConcurrencyLimit = 1;
            });
        }
    }

    public class DoWorkConsumer : IConsumer<Batch<DoWork>>
    {
        private readonly ILogger<DoWorkConsumer> logger;

        public DoWorkConsumer(ILogger<DoWorkConsumer> logger)
        {
            this.logger = logger;
        }

        public async Task Consume(ConsumeContext<Batch<DoWork>> context)
        {
            logger.LogInformation("Consumed {0} DoWork: {1}", context.Message.Length, string.Join(",", context.Message.Select(x => x.MessageId).ToArray()));

            throw new Exception("error");
        }
    }
}