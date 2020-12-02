using System;
using System.Threading.Tasks;
using GreenPipes;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace MassTransitTest.UnitOfWork
{
    public class UnitOfWorkFilter<TUnitOfWork, TContext, TConsumer> : IFilter<TContext>
        where TConsumer : class
        where TContext : class, ConsumerConsumeContext<TConsumer>
    {
        private readonly Func<TUnitOfWork, Task> complete;
        private readonly Func<TUnitOfWork, Task> onError;

        public UnitOfWorkFilter(Func<TUnitOfWork, Task> complete, Func<TUnitOfWork, Task> onError = null)
        {
            this.complete = complete ?? throw new ArgumentNullException(nameof(complete));
            this.onError = onError;
        }

        public void Probe(ProbeContext context)
        {
            context.CreateFilterScope("uow");
        }

        public async Task Send(TContext context, IPipe<TContext> next)
        {
            var provider = context.GetPayload<IServiceProvider>();
            var unitOfWork = provider.GetRequiredService<TUnitOfWork>();

            try
            {
                await next.Send(context);
                await complete(unitOfWork);
            }
            catch (Exception)
            {
                if (onError != null)
                {
                    await onError(unitOfWork);
                }
                throw;
            }
        }
    }
}
