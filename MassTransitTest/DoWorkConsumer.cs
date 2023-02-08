using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace MassTransitTest;

public class DoWork
{
}

public class DoWorkConsumer : IConsumer<Batch<DoWork>>
{
    private readonly ILogger<DoWorkConsumer> logger;

    public DoWorkConsumer(ILogger<DoWorkConsumer> logger)
    {
        this.logger = logger;
    }

    public Task Consume(ConsumeContext<Batch<DoWork>> context)
    {
        logger.LogDebug("Working {0}", nameof(DoWorkConsumer));
        return Task.CompletedTask;
    }
}
