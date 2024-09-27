using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Enums;
using TwitchScanAPI.Models.Twitch;

namespace TwitchScanAPI.Data.Statistics
{
    public class SubscriptionStatistic : IStatistic
    {
        public string Name => "SubscriptionStatistic";

        private readonly ConcurrentDictionary<SubscriptionType, int> _subscriptionCounts = new();
        private readonly ConcurrentDictionary<string, int> _subscriberMonths = new();
        private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new();

        public object GetResult()
        {
            return new
            {
                TotalNewSubscribers = _subscriptionCounts.GetValueOrDefault(SubscriptionType.New),
                TotalReSubscribers = _subscriptionCounts.GetValueOrDefault(SubscriptionType.Re),
                TotalGiftedSubscriptions = _subscriptionCounts.GetValueOrDefault(SubscriptionType.Gifted),
                AverageSubscriptionMonths = _subscriberMonths.Count > 0 ? _subscriberMonths.Values.Average() : 0,
                TopSubscribers = _subscriberMonths
                    .OrderByDescending(kv => kv.Value)
                    .Take(10)
                    .ToDictionary(kv => kv.Key, kv => kv.Value),
                Subscriptions = _subscriptions.Values
            };
        }

        public void Update(Subscription subscription)
        {
            // Track all subscriptions
            _subscriptions.AddOrUpdate(subscription.UserName, subscription, (key, oldValue) => subscription);
            
            // Increment count based on the subscription type
            _subscriptionCounts.AddOrUpdate(subscription.Type, 1, (type, count) => count + 1);

            // Track months for resubscribers and gifted subscriptions if applicable
            _subscriberMonths.AddOrUpdate(subscription.UserName, subscription.Months, (key, oldValue) => oldValue + subscription.Months);
        }
    }
}