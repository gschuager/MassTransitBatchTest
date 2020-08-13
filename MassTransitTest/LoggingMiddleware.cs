using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using GreenPipes;
using GreenPipes.Internals.Extensions;
using GreenPipes.Specifications;
using MassTransit;
using MassTransit.ConsumeConfigurators;
using MassTransit.Internals.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using InterfaceExtensions = MassTransit.Internals.Extensions.InterfaceExtensions;

namespace MassTransitTest
{
    public class LoggingFilter2<TConsumer> : IFilter<ConsumerConsumeContext<TConsumer>>
        where TConsumer : class
    {
        public void Probe(ProbeContext context)
        {
            context.CreateFilterScope("logging");
        }

        public async Task Send(ConsumerConsumeContext<TConsumer> context, IPipe<ConsumerConsumeContext<TConsumer>> next)
        {
            var messageType = context.GetType().GetClosingArgument(typeof(ConsumeContext<>));

            int? length = null;
            BatchCompletionMode? mode = null;
            if (InterfaceExtensions.ClosesType(messageType, typeof(Batch<>)))
            {
                var consumeContextType = typeof(ConsumeContext<>).MakeGenericType(messageType);

                var message = consumeContextType.GetProperty("Message")!.GetValue(context);

                length = (int)messageType.GetProperty(nameof(Batch<object>.Length))!.GetValue(message)!;
                mode = (BatchCompletionMode)messageType.GetProperty(nameof(Batch<object>.Mode))!.GetValue(message)!;
            }

            var serviceProvider = context.GetPayload<IServiceProvider>();
            var logger = serviceProvider.GetRequiredService<ILogger<LoggingFilter2<TConsumer>>>();

            using (logger.BeginScope(new Dictionary<string, object> { {"messageId", context.MessageId} }))
            {
                if (length == null)
                {
                    logger.LogInformation("┌── {0}", TypeCache<TConsumer>.ShortName);
                }
                else
                {
                    logger.LogInformation("╔══ {0} - {1} - {2} messages", TypeCache<TConsumer>.ShortName, mode, length);
                }
                
                var sw = Stopwatch.StartNew();
                try
                {
                    await next.Send(context);
                }
                finally
                {
                    if (length == null)
                    {
                        logger.LogInformation("└── {0} [{1} ms]", TypeCache<TConsumer>.ShortName, sw.ElapsedMilliseconds);
                    }
                    else
                    {
                        logger.LogInformation("╚══ {0} [{1} ms]", TypeCache<TConsumer>.ShortName, sw.ElapsedMilliseconds);
                    }
                }
            }
        }
    }

    public class LoggingConfigurationObserver :
        IConsumerConfigurationObserver
    {
        public void ConsumerConfigured<TConsumer>(IConsumerConfigurator<TConsumer> configurator) where TConsumer : class
        {
            configurator.AddPipeSpecification(
                new FilterPipeSpecification<ConsumerConsumeContext<TConsumer>>(new LoggingFilter2<TConsumer>()));
        }

        public void ConsumerMessageConfigured<TConsumer, TMessage>(
            IConsumerMessageConfigurator<TConsumer, TMessage> configurator)
            where TConsumer : class
            where TMessage : class
        {
        }
    }

    public static class LoggingMiddlewareConfiguratorExtensions
    {
        public static void UseLogging(this IConsumePipeConfigurator configurator)
        {
            configurator.ConnectConsumerConfigurationObserver(new LoggingConfigurationObserver());
        }
    }
}
