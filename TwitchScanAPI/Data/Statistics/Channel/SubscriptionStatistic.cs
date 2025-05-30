﻿using System;
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
    public class SubscriptionStatistic : StatisticBase
    {
        private const int BucketSize = 1; // Grouping subscriptions into 1-minute periods

        // Tracks the count of each SubscriptionType
        private ConcurrentDictionary<SubscriptionType, int> _subscriptionCounts = new();

        // Tracks the number of subscriptions over each minute interval
        private ConcurrentDictionary<string, long> _subscriptionsOverTime = new();

        // Tracks the total subscription months per subscriber (for gifted subscriptions)
        private ConcurrentDictionary<string, int> _topSubscriber = new(StringComparer.OrdinalIgnoreCase);

        public override string Name => "SubscriptionStatistic";

        protected override object ComputeResult()
        {
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

            return new SubscriptionStatisticResult
            {
                // Community subscriptions are not included in the total subscriber count
                // They represent the action of gifting an anonymous subscription to the community, not a specific user
                TotalSubscribers = _subscriptionCounts.Where(sub => sub.Key != SubscriptionType.Community)
                    .Sum(sub => sub.Value),
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
            {
                _topSubscriber.AddOrUpdate(channelSubscription.UserName, channelSubscription.Months,
                    (_, oldValue) => oldValue + channelSubscription.Months);
            }

            // Track subscriptions over time (batched by minute)
            var currentTime = DateTime.UtcNow;
            UpdateSubscriptionsOverTime(currentTime);
            HasUpdated = true;
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

        public override void Dispose()
        {
            base.Dispose();
            _subscriptionCounts = new ConcurrentDictionary<SubscriptionType, int>();
            _subscriptionsOverTime = new ConcurrentDictionary<string, long>();
            _topSubscriber = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }
}