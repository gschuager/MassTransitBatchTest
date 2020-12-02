using System;
using System.Threading.Tasks;
using MassTransit;

namespace MassTransitTest.UnitOfWork
{
    public interface IUnitOfWork
    {
        void Add(object @event);
        Task Complete(IPublishEndpoint publishEndpoint);
    }
}