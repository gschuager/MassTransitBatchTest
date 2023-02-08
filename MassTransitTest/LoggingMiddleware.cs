using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Internals;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MassTransitTest;

public class LoggingFilter<TConsumer> : IFilter<ConsumerConsumeContext<TConsumer>> where TConsumer : class
{
    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("logging");
    }

    public async Task Send(ConsumerConsumeContext<TConsumer> context, IPipe<ConsumerConsumeContext<TConsumer>> next)
    {
        var messageType = context.GetType().GetClosingArgument(typeof(ConsumeContext<>));

        int? length = null;
        BatchCompletionMode? mode = null;
        if (messageType.ClosesType(typeof(Batch<>)))
        {
            var consumeContextType = typeof(ConsumeContext<>).MakeGenericType(messageType);

            var message = consumeContextType.GetProperty("Message")!.GetValue(context);

            length = (int)messageType.GetProperty(nameof(Batch<object>.Length))!.GetValue(message)!;
            mode = (BatchCompletionMode)messageType.GetProperty(nameof(Batch<object>.Mode))!.GetValue(message)!;
        }

        var serviceProvider = context.GetPayload<IServiceProvider>();
        var logger = serviceProvider.GetRequiredService<ILogger<LoggingFilter<TConsumer>>>();

        using (logger.BeginScope(new Dictionary<string, object> { { "messageId", context.MessageId! } }))
        {
            if (length == null)
            {
                logger.LogInformation("┌── {0}", TypeCache<TConsumer>.ShortName);
            }
            else
            {
                logger.LogInformation("╔══ {0} - {1} - {2} messages", TypeCache<TConsumer>.ShortName, mode, length);
            }

            var sw = Stopwatch.StartNew();
            try
            {
                await next.Send(context);
            }
            finally
            {
                var ellapsedTime = sw.Elapsed;

                if (length == null)
                {
                    logger.LogInformation(
                        "└── {0} [{1} ms]",
                        TypeCache<TConsumer>.ShortName,
                        ellapsedTime.TotalMilliseconds
                    );
                }
                else
                {
                    logger.LogInformation(
                        "╚══ {0} [{1} ms]",
                        TypeCache<TConsumer>.ShortName,
                        ellapsedTime.TotalMilliseconds
                    );
                }
            }
        }
    }
}

