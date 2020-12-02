using System;
using System.Threading.Tasks;
using GreenPipes.Specifications;
using MassTransit;
using MassTransit.ConsumeConfigurators;
using MassTransit.PipeConfigurators;

namespace MassTransitTest.UnitOfWork
{
    public class UnitOfWorkConfigurationObserver<TUnitOfWork> :
        ConfigurationObserver,
        IMessageConfigurationObserver
    {
        readonly Func<ConsumeContext, TUnitOfWork, Task> complete;
        readonly Func<TUnitOfWork, Task> onError;

        public UnitOfWorkConfigurationObserver(IConsumePipeConfigurator configurator, Func<ConsumeContext, TUnitOfWork, Task> complete, Func<TUnitOfWork, Task>
            onError)
            : base(configurator)
        {
            this.complete = complete ?? throw new ArgumentNullException(nameof(complete));
            this.onError = onError;

            Connect(this);
        }

        public void MessageConfigured<TMessage>(IConsumePipeConfigurator configurator)
            where TMessage : class
        {
            // var filter = new UnitOfWorkFilter<TUnitOfWork, ConsumeContext<TMessage>>(complete, onError);
            // var specification = new FilterPipeSpecification<ConsumeContext<TMessage>>(filter);
            // configurator.AddPipeSpecification(specification);
        }

        public override void BatchConsumerConfigured<TConsumer, TMessage>(IConsumerMessageConfigurator<TConsumer, Batch<TMessage>> configurator)
        {
            var filter = new UnitOfWorkFilter<TUnitOfWork, ConsumeContext<Batch<TMessage>>>(complete, onError);
            var specification = new FilterPipeSpecification<ConsumeContext<Batch<TMessage>>>(filter);

            configurator.Message(x => x.AddPipeSpecification(specification));
        }
    }
}