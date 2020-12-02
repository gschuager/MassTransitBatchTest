using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit;

namespace MassTransitTest.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ConsumeContext context;
        private readonly List<object> events = new List<object>();

        public UnitOfWork(ConsumeContext context)
        {
            this.context = context;
        }
        
        public void Add(object @event)
        {
            events.Add(@event);
        }

        public async Task Complete()
        {
            foreach (var e in events)
            {
                await context.Publish(e);
            }

            await Task.Delay(500);
            
            // commits db transaction here
        }
    }
}