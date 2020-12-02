using System;
using System.Threading.Tasks;

namespace MassTransitTest.UnitOfWork
{
    public interface IUnitOfWork
    {
        void Add(object @event);
        Task Complete();
    }
}