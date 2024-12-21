using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Enums;
using TwitchScanAPI.Models.Twitch.Channel;
using TwitchScanAPI.Models.Twitch.Statistics;
using TwitchScanAPI.Services;

namespace TwitchScanAPI.Data.Statistics.Channel
{
    public class SubscriptionStatistic : IStatistic
    {
        private const int BucketSize = 1; // Grouping subscriptions into 1-minute periods

        // Tracks the count of each SubscriptionType
        private readonly ConcurrentDictionary<SubscriptionType, int> _subscriptionCounts = new();

        // Tracks the number of subscriptions over each minute interval
        private readonly ConcurrentDictionary<string, long> _subscriptionsOverTime = new();

        // Tracks the total subscription months per subscriber (for gifted subscriptions)
        private readonly ConcurrentDictionary<string, int> _topSubscriber = new(StringComparer.OrdinalIgnoreCase);
        public string Name => "SubscriptionStatistic";

        public object GetResult()
        {
            var currentTime = DateTime.UtcNow;

            // Aggregate subscriptions over time, ordered chronologically
            var subscriptionsOverTime = _subscriptionsOverTime
                .OrderBy(kvp => kvp.Key)
                .ToList();

            // Calculate the Trend
            var trend = TrendService.CalculateTrend(
                subscriptionsOverTime,
                d => d.Value,
                d => DateTime.Parse(d.Key)
            );

            // Return the result with all necessary metrics
            return new SubscriptionStatisticResult
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
                SubscriptionsOverTime = subscriptionsOverTime,
                Trend = trend
            };
        }

        public Task Update(ChannelSubscription channelSubscription)
        {
            // Increment count based on the subscription type
            _subscriptionCounts.AddOrUpdate(channelSubscription.Type, 1, (_, count) => count + 1);

            // Track subscription months for gifted subscriptions
            if (channelSubscription.Type == SubscriptionType.Gifted &&
                !string.IsNullOrWhiteSpace(channelSubscription.UserName))
                _topSubscriber.AddOrUpdate(channelSubscription.UserName, channelSubscription.Months,
                    (_, oldValue) => oldValue + channelSubscription.Months);

            // Track subscriptions over time (batched by minute)
            var currentTime = DateTime.UtcNow;
            UpdateSubscriptionsOverTime(currentTime);
            return Task.CompletedTask;
        }

        private void UpdateSubscriptionsOverTime(DateTime timestamp)
        {
            // Round the timestamp to the nearest minute
            var roundedMinutes = Math.Floor((double)timestamp.Minute / BucketSize) * BucketSize;
            var roundedTime = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour,
                    (int)roundedMinutes, 0)
                .ToString("yyyy-MM-ddTHH:mm:ssZ");

            // Add or update the subscription count for the time bucket
            _subscriptionsOverTime.AddOrUpdate(roundedTime, 1, (_, count) => count + 1);
        }
    }
}