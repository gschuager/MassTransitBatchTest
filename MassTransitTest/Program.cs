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
using System.Reflection;
using System.Threading.Tasks;
using MassTransit.RabbitMqTransport;
using MassTransitTest.UnitOfWork;

namespace MassTransitTest
{
    class Program
    {
        public static async Task Main(string[] args)
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

                await bus.Send(new DoWork());
                await bus.Send(new DoWork());

                await Task.Delay(4000);
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
            services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();

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

            cfg.UseLogging();
            cfg.UseInMemoryOutbox();
            cfg.UseUnitOfWork<IUnitOfWork>(uow => uow.Complete());

            cfg.ConfigureEndpoints(context);
        }
    }
}