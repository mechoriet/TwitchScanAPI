using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Enums;
using TwitchScanAPI.Models.Twitch.Channel;

namespace TwitchScanAPI.Data.Statistics.Channel
{
    public class SubscriptionStatistic : IStatistic
    {
        public string Name => "SubscriptionStatistic";

        private readonly ConcurrentDictionary<SubscriptionType, int> _subscriptionCounts = new();
        private readonly ConcurrentDictionary<string, int> _topSubscriber = new();

        public object GetResult()
        {
            return new
            {
                TotalSubscribers = _subscriptionCounts.Values.Sum(),
                TotalNewSubscribers = _subscriptionCounts.GetValueOrDefault(SubscriptionType.New),
                TotalReSubscribers = _subscriptionCounts.GetValueOrDefault(SubscriptionType.Re),
                TotalGiftedSubscriptions = _subscriptionCounts.GetValueOrDefault(SubscriptionType.Gifted),
                TotalCommunitySubscriptions = _subscriptionCounts.GetValueOrDefault(SubscriptionType.Community),
                AverageSubscriptionMonths = !_topSubscriber.IsEmpty ? _topSubscriber.Values.Average() : 0,
                TopSubscribers = _topSubscriber
                    .OrderByDescending(kv => kv.Value)
                    .Take(10)
                    .ToDictionary(kv => kv.Key, kv => kv.Value),
            };
        }

        public void Update(ChannelSubscription channelSubscription)
        {
            // Increment count based on the channelSubscription type
            _subscriptionCounts.AddOrUpdate(channelSubscription.Type, 1, (_, count) => count + 1);

            // Track months for resubscribers and gifted subscriptions if applicable
            if (channelSubscription.Type == SubscriptionType.Gifted)
                _topSubscriber.AddOrUpdate(channelSubscription.UserName, channelSubscription.Months,
                    (_, oldValue) => oldValue + channelSubscription.Months);
        }
    }
}