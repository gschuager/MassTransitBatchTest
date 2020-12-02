using System;
using System.Threading.Tasks;
using GreenPipes;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace MassTransitTest.UnitOfWork
{
    public class UnitOfWorkFilter<TUnitOfWork, TContext> : IFilter<TContext>
        where TContext : class, ConsumeContext
    {
        private readonly Func<ConsumeContext, TUnitOfWork, Task> complete;
        private readonly Func<TUnitOfWork, Task> onError;

        public UnitOfWorkFilter(Func<ConsumeContext, TUnitOfWork, Task> complete, Func<TUnitOfWork, Task> onError = null)
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
                await complete(context, unitOfWork);
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