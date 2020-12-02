using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit;

namespace MassTransitTest.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        readonly List<object> events = new List<object>();

        public void Add(object @event)
        {
            events.Add(@event);
        }

        public async Task Complete(IPublishEndpoint publishEndpoint)
        {
            foreach (var e in events) await publishEndpoint.Publish(e);

            await Task.Delay(500);

            // commits db transaction here
        }
    }
}