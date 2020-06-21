using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MassTransitTest
{
    public static class ActionsListExtensions
    {
        public static async Task ExecuteSequentially(this IEnumerable<Func<Task>> actions)
        {
            foreach (var action in actions)
            {
                await action().ConfigureAwait(false);
            }
        }
        
        public static async Task ExecuteConcurrently(this IEnumerable<Func<Task>> actions, int concurrencyLimit)
        {
            await Task.Yield();
            var actions1 = actions as Func<Task>[] ?? actions.ToArray();

            var stripes = new List<Task>();
            var actionsPerStripe = (int) Math.Ceiling((decimal) actions1.Length / concurrencyLimit);

            for (var i = 0; i < concurrencyLimit; i++)
            {
                var stripeActions = actions1.Skip(i * actionsPerStripe).Take(actionsPerStripe).ToArray();
                if (stripeActions.Length == 0)
                    break;

                stripes.Add(RunStripe(stripeActions));
            }

            await Task.WhenAll(stripes).ConfigureAwait(false);
        }

        private static async Task RunStripe(IEnumerable<Func<Task>> actions)
        {
            await Task.Yield();

            foreach (var action in actions)
            {
                await action().ConfigureAwait(false);
            }
        }
    }
}