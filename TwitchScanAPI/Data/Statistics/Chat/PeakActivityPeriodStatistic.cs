using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class PeakActivityPeriodStatistic : IStatistic
    {
        public string Name => "PeakActivityPeriods";
        private readonly ConcurrentDictionary<string, int> _hourlyMessageCounts = new();

        private readonly TimeSpan _retentionPeriod = TimeSpan.FromHours(24);
        private const int BucketSize = 1;

        private readonly Timer _cleanupTimer;

        public PeakActivityPeriodStatistic()
        {
            // Initialize the timer to trigger cleanup every hour
            _cleanupTimer = new Timer(3600000); // 3600000 ms = 1 hour
            _cleanupTimer.Elapsed += (sender, e) => CleanupOldData();
            _cleanupTimer.AutoReset = true;
            _cleanupTimer.Start();
        }

        public object GetResult()
        {
            return _hourlyMessageCounts
                .OrderByDescending(kv => kv.Value)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public void Update(ChannelMessage message)
        {
            if (message?.Time == null) return; // Handle null message or time

            // Get the time in UTC format
            var dateTime = message.Time;

            // Round the minutes to the nearest 5-minute period
            var roundedMinutes = Math.Floor((double)dateTime.Minute / BucketSize) * BucketSize;

            // Create a new DateTime with the rounded minutes
            var roundedTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour,
                    (int)roundedMinutes, 0)
                .ToString("yyyy-MM-ddTHH:mm:ss");

            // Add or update the count for the current rounded time in a thread-safe manner
            _hourlyMessageCounts.AddOrUpdate(roundedTime, 1, (key, oldValue) => oldValue + 1);
        }

        private void CleanupOldData()
        {
            // Calculate the expiration time
            var expirationTime = DateTime.UtcNow.Subtract(_retentionPeriod);

            // List to hold the keys that should be removed
            var keysToRemove = new List<string>();

            foreach (var key in _hourlyMessageCounts.Keys)
            {
                // Parse the key back to DateTime to compare it
                if (!DateTime.TryParse(key, out var timeKey)) continue;
                // If the time is older than the expiration time, mark it for removal
                if (timeKey < expirationTime)
                {
                    keysToRemove.Add(key);
                }
            }

            // Remove expired entries
            foreach (var key in keysToRemove)
            {
                _hourlyMessageCounts.TryRemove(key, out _);
            }
        }
    }
}