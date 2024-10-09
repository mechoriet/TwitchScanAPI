using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using TwitchLib.Client.Models;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Statistics;
using TwitchScanAPI.Services;

namespace TwitchScanAPI.Data.Statistics.Channel
{
    public class RaidStatistic : IStatistic
    {
        public string Name => "RaidStatistic";

        // Tracks the count of raids
        private readonly ConcurrentDictionary<string, int> _raidCounts = new(StringComparer.OrdinalIgnoreCase);

        // Tracks raids over time (bucketed by minute)
        private readonly ConcurrentDictionary<string, long> _raidsOverTime = new();
        private readonly TimeSpan _retentionPeriod = TimeSpan.FromHours(48);
        private const int BucketSize = 1; // Grouping raids into 1-minute periods
        private readonly Timer _cleanupTimer;

        public RaidStatistic()
        {
            // Initialize the timer to trigger cleanup every hour
            _cleanupTimer = new Timer(3600000); // 3600000 ms = 1 hour
            _cleanupTimer.Elapsed += (_, _) => CleanupOldData();
            _cleanupTimer.AutoReset = true;
            _cleanupTimer.Start();
        }

        public object GetResult()
        {
            var currentTime = DateTime.UtcNow;
            var cutoffTime = currentTime - _retentionPeriod;

            // Aggregate raids over time, ordered chronologically
            var raidsOverTime = _raidsOverTime
                .Where(kvp => DateTime.Parse(kvp.Key) >= cutoffTime)
                .OrderBy(kvp => kvp.Key)
                .ToList();

            // Calculate the Trend
            var trend = TrendService.CalculateTrend(
                raidsOverTime,
                d => d.Value,
                d => DateTime.Parse(d.Key)
            );

            // Return the result with all necessary metrics
            return new RaidStatisticResult
            {
                TotalRaids = _raidCounts.Values.Sum(),
                TopRaiders = _raidCounts
                    .OrderByDescending(kv => kv.Value)
                    .Take(10)
                    .ToDictionary(kv => kv.Key, kv => kv.Value),
                RaidsOverTime = raidsOverTime,
                Trend = trend
            };
        }

        public void Update(RaidNotification raidNotification)
        {
            // Increment raid count for the raider
            _raidCounts.AddOrUpdate(raidNotification.MsgParamLogin, 1, (_, count) => count + 1);

            // Track the raid over time (batched by minute)
            var currentTime = DateTime.UtcNow;
            UpdateRaidsOverTime(currentTime);
        }

        private void UpdateRaidsOverTime(DateTime timestamp)
        {
            // Round the timestamp to the nearest minute
            var roundedMinutes = Math.Floor((double)timestamp.Minute / BucketSize) * BucketSize;
            var roundedTime = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour,
                    (int)roundedMinutes, 0)
                .ToString("yyyy-MM-ddTHH:mm:ssZ");

            // Add or update the raid count for the time bucket
            _raidsOverTime.AddOrUpdate(roundedTime, 1, (_, count) => count + 1);
        }

        private void CleanupOldData()
        {
            // Calculate the expiration time
            var expirationTime = DateTime.UtcNow.Subtract(_retentionPeriod);

            // List to hold the keys that should be removed
            var keysToRemove = new List<string>();

            foreach (var key in _raidsOverTime.Keys)
            {
                if (DateTime.TryParse(key, out var timeKey) && timeKey < expirationTime)
                {
                    keysToRemove.Add(key);
                }
            }

            // Remove expired entries
            foreach (var key in keysToRemove)
            {
                _raidsOverTime.TryRemove(key, out _);
            }
        }
    }
}
