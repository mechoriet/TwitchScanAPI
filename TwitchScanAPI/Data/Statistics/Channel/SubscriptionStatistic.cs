using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TwitchScanAPI.Data.Statistics.Chat.Base;
using TwitchScanAPI.Models.Enums;
using TwitchScanAPI.Models.Twitch;
using TwitchScanAPI.Models.Twitch.Channel;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class SubscriptionStatistic : IStatistic
    {
        public string Name => "SubscriptionStatistic";

        private readonly ConcurrentDictionary<SubscriptionType, int> _subscriptionCounts = new();
        private readonly ConcurrentDictionary<string, int> _subscriberMonths = new();

        public object GetResult()
        {
            return new
            {
                TotalSubscribers = _subscriptionCounts.Values.Sum(),
                TotalNewSubscribers = _subscriptionCounts.GetValueOrDefault(SubscriptionType.New),
                TotalReSubscribers = _subscriptionCounts.GetValueOrDefault(SubscriptionType.Re),
                TotalGiftedSubscriptions = _subscriptionCounts.GetValueOrDefault(SubscriptionType.Gifted),
                AverageSubscriptionMonths = !_subscriberMonths.IsEmpty ? _subscriberMonths.Values.Average() : 0,
                TopSubscribers = _subscriberMonths
                    .OrderByDescending(kv => kv.Value)
                    .Take(10)
                    .ToDictionary(kv => kv.Key, kv => kv.Value),
            };
        }

        public void Update(ChannelSubscription channelSubscription)
        {
            // Increment count based on the channelSubscription type
            _subscriptionCounts.AddOrUpdate(channelSubscription.Type, 1, (type, count) => count + 1);

            // Track months for resubscribers and gifted subscriptions if applicable
            _subscriberMonths.AddOrUpdate(channelSubscription.UserName, channelSubscription.Months, (key, oldValue) => oldValue + channelSubscription.Months);
        }
    }
}