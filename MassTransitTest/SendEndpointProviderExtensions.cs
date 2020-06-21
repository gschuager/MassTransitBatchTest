using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;

namespace MassTransitTest
{
    public static class SendEndpointProviderExtensions
    {
        public static async Task SendConcurrently(this ISendEndpointProvider sendEndpointProvider,
            IEnumerable<object> messages,
            int concurrencyLimit)
        {
            await Task.Yield();
            var messages1 = messages as object[] ?? messages.ToArray();

            var stripes = new List<Task>();
            var messagesPerStripe = (int)Math.Ceiling((decimal)messages1.Length / concurrencyLimit);

            for (var i = 0; i < concurrencyLimit; i++)
            {
                var stripeMessages = messages1.Skip(i * messagesPerStripe).Take(messagesPerStripe).ToArray();
                if (stripeMessages.Length == 0)
                    break;

                stripes.Add(RunStripe(sendEndpointProvider, stripeMessages));
            }

            await Task.WhenAll(stripes).ConfigureAwait(false);
        }

        private static async Task RunStripe(ISendEndpointProvider sendEndpointProvider, IEnumerable<object> messages)
        {
            await Task.Yield();

            foreach (var message in messages)
            {
                await sendEndpointProvider.Send(message).ConfigureAwait(false);
            }
        }
    }
}