using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace MassTransitTest
{
    public class DomainEvent
    {
    }
    
    public class DomainEventConsumer : IConsumer<DomainEvent>
    {
        private readonly ILogger<DomainEventConsumer> logger;

        public DomainEventConsumer(ILogger<DomainEventConsumer> logger)
        {
            this.logger = logger;
        }

        public async Task Consume(ConsumeContext<DomainEvent> context)
        {
            logger.LogDebug("Domain event consumed");
        }
    }
}