using MassTransit;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using MassTransitTest.UnitOfWork;

namespace MassTransitTest
{
    public class DoWork
    {
    }

    public class DoWorkConsumer : IConsumer<Batch<DoWork>>
    {
        private readonly ILogger<DoWorkConsumer> logger;
        private readonly IUnitOfWork unitOfWork;

        public DoWorkConsumer(ILogger<DoWorkConsumer> logger, IUnitOfWork unitOfWork)
        {
            this.logger = logger;
            this.unitOfWork = unitOfWork;
        }

        public async Task Consume(ConsumeContext<Batch<DoWork>> context)
        {
            unitOfWork.Add(new DomainEvent());
            
            logger.LogDebug("Working");
        }
    }
}