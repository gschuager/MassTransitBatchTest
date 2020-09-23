using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MassTransitTest
{
    public class MessageCounter2
    {
        private readonly ILogger<MessageCounter2> logger;
        private readonly IDictionary<string, HashSet<Guid>> consumedIds = new Dictionary<string, HashSet<Guid>>();

        public MessageCounter2(ILogger<MessageCounter2> logger)
        {
            this.logger = logger;
        }

        public void Consumed(string key, IEnumerable<Guid> receivedIds)
        {
            lock (consumedIds)
            {
                if (consumedIds.TryGetValue(key, out var trackedIds) == false)
                {
                    trackedIds = new HashSet<Guid>();
                    consumedIds.Add(key, trackedIds);
                }

                foreach (var id in receivedIds)
                {
                    trackedIds.Add(id);
                }

                logger.LogInformation("    - {0}", string.Join(" ", consumedIds.Select(x => $"{x.Key} ({x.Value.Count})")));
            }
        }
    }
}