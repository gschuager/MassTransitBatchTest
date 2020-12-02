using System;
using System.Threading.Tasks;
using GreenPipes.Specifications;
using MassTransit;
using MassTransit.ConsumeConfigurators;
using MassTransit.Internals.Extensions;

namespace MassTransitTest.UnitOfWork
{
    public class UnitOfWorkConfigurationObserver<TUnitOfWork> :
        IConsumerConfigurationObserver
    {
        private readonly Func<TUnitOfWork, Task> complete;
        private readonly Func<TUnitOfWork, Task> onError;

        public UnitOfWorkConfigurationObserver(Func<TUnitOfWork, Task> complete, Func<TUnitOfWork, Task> onError)
        {
            this.complete = complete ?? throw new ArgumentNullException(nameof(complete));
            this.onError = onError;
        }

        public void ConsumerConfigured<TConsumer>(IConsumerConfigurator<TConsumer> configurator) where TConsumer : class
        {
            var filter = new UnitOfWorkFilter<TUnitOfWork, ConsumerConsumeContext<TConsumer>, TConsumer>(complete, onError);
            var specification = new FilterPipeSpecification<ConsumerConsumeContext<TConsumer>>(filter);
            configurator.AddPipeSpecification(specification);
        }

        public void ConsumerMessageConfigured<TConsumer, TMessage>(IConsumerMessageConfigurator<TConsumer, TMessage> configurator)
            where TConsumer : class
            where TMessage : class
        {
        }
    }
}
