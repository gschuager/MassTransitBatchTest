using System.Threading.Tasks;
using GreenPipes;
using GreenPipes.Internals.Extensions;
using GreenPipes.Specifications;
using MassTransit;
using MassTransit.ConsumeConfigurators;
using MassTransit.Context;

namespace MassTransitTest
{
    public class LoggingFilter<TConsumer> : IFilter<ConsumerConsumeContext<TConsumer>>
        where TConsumer : class
    {
        private readonly ILogContext logContext;

        public LoggingFilter()
        {
            logContext = LogContext.CreateLogContext(typeof(LoggingFilter<>).FullName);
        }
        
        public void Probe(ProbeContext context)
        {
            context.CreateFilterScope("logging");
        }

        public async Task Send(ConsumerConsumeContext<TConsumer> context, IPipe<ConsumerConsumeContext<TConsumer>> next)
        {
            var scope = logContext.BeginScope();
            try
            {
                scope?.Add("messageId", context.MessageId);
                logContext.Info?.Log("┌── Begin {0}", TypeCache<TConsumer>.ShortName);
                await next.Send(context);
            }
            finally
            {
                logContext.Info?.Log("└── Finished {0}", TypeCache<TConsumer>.ShortName);
                scope?.Dispose();
            }
        }
    }

    public class LoggingConfigurationObserver :
        IConsumerConfigurationObserver
    {
        public void ConsumerConfigured<TConsumer>(IConsumerConfigurator<TConsumer> configurator) where TConsumer : class
        {
            configurator.AddPipeSpecification(
                new FilterPipeSpecification<ConsumerConsumeContext<TConsumer>>(new LoggingFilter<TConsumer>()));
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
