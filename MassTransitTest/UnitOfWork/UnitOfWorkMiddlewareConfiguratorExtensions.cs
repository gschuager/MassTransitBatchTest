using System;
using System.Threading.Tasks;
using MassTransit;

namespace MassTransitTest.UnitOfWork
{
    public static class UnitOfWorkMiddlewareConfiguratorExtensions
    {
        public static void UseUnitOfWork<TUnitOfWork>(
            this IConsumePipeConfigurator configurator,
            Func<TUnitOfWork, Task> complete,
            Func<TUnitOfWork, Task>? onError = null
        ) where TUnitOfWork : notnull
        {
            if (complete == null)
            {
                throw new ArgumentNullException(nameof(complete));
            }

            configurator.ConnectConsumerConfigurationObserver(
                new UnitOfWorkConfigurationObserver<TUnitOfWork>(complete, onError)
            );
        }
    }
}