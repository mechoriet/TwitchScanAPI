using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using TwitchLib.Client.Models;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;
using TwitchScanAPI.Models.Twitch.Statistics;
using TwitchScanAPI.Services;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class PeakActivityPeriodStatistic : IStatistic
    {
        public string Name => "PeakActivityPeriods";

        // Dictionaries for tracking message counts in different channel states
        private readonly ConcurrentDictionary<DateTime, long> _messagesOverTime = new();
        private readonly ConcurrentDictionary<DateTime, long> _subOnlyMessagesOverTime = new();
        private readonly ConcurrentDictionary<DateTime, long> _emoteOnlyMessagesOverTime = new();
        private readonly ConcurrentDictionary<DateTime, long> _slowOnlyMessagesOverTime = new();

        // Retention period to keep only relevant data (48 hours)
        private readonly TimeSpan _retentionPeriod = TimeSpan.FromHours(48);
        private const int BucketSize = 1; // Grouping messages into 1-minute periods

        // Timer for periodic cleanup
        private readonly Timer _cleanupTimer;

        // Stores the current channel state
        private ChannelState? _channelState;

        public PeakActivityPeriodStatistic()
        {
            // Initialize the timer for hourly cleanup
            _cleanupTimer = new Timer(3600000); // 1 hour in milliseconds
            _cleanupTimer.Elapsed += (_, _) => CleanupOldData();
            _cleanupTimer.AutoReset = true;
            _cleanupTimer.Start();
        }

        /// <summary>
        /// Returns the result of tracked message counts, including general messages and messages in sub-only, emote-only, and slow modes.
        /// </summary>
        public object GetResult()
        {
            // Calculate messages
            var completeData = _messagesOverTime
                .Concat(_subOnlyMessagesOverTime)
                .Concat(_emoteOnlyMessagesOverTime)
                .Concat(_slowOnlyMessagesOverTime)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            var trend = TrendService.CalculateTrend(
                completeData,
                d => d.Value,
                d => d.Key
            );
            
            return PeakActivityPeriods.Create(trend,
                _messagesOverTime,
                _subOnlyMessagesOverTime,
                _emoteOnlyMessagesOverTime,
                _slowOnlyMessagesOverTime
            );
        }

        /// <summary>
        /// Updates the message count based on the received channel message, considering the current channel state (e.g., sub-only mode).
        /// </summary>
        public void Update(ChannelMessage message)
        {
            // Get the message timestamp
            var dateTime = message.Time;

            // Round the time to the nearest minute (based on BucketSize)
            var roundedMinutes = Math.Floor((double)dateTime.Minute / BucketSize) * BucketSize;
            var roundedTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour,
                (int)roundedMinutes, 0);

            // Increment message count in the appropriate dictionary based on the channel state
            if (_channelState?.SubOnly == true)
                _subOnlyMessagesOverTime.AddOrUpdate(roundedTime, 1, (_, oldValue) => oldValue + 1);
            else if (_channelState?.EmoteOnly == true)
                _emoteOnlyMessagesOverTime.AddOrUpdate(roundedTime, 1, (_, oldValue) => oldValue + 1);
            else if (_channelState?.SlowMode is > 0)
                _slowOnlyMessagesOverTime.AddOrUpdate(roundedTime, 1, (_, oldValue) => oldValue + 1);
            else
                _messagesOverTime.AddOrUpdate(roundedTime, 1, (_, oldValue) => oldValue + 1);
        }

        /// <summary>
        /// Updates the internal channel state, which is used to determine how messages are tracked (e.g., sub-only mode).
        /// </summary>
        public void Update(ChannelState channelState)
        {
            _channelState = channelState;
        }

        /// <summary>
        /// Cleans up data older than the retention period (48 hours) to ensure that the dictionaries do not grow indefinitely.
        /// </summary>
        private void CleanupOldData()
        {
            var expirationTime = DateTime.UtcNow.Subtract(_retentionPeriod);

            CleanupDictionary(_messagesOverTime, expirationTime);
            CleanupDictionary(_subOnlyMessagesOverTime, expirationTime);
            CleanupDictionary(_emoteOnlyMessagesOverTime, expirationTime);
            CleanupDictionary(_slowOnlyMessagesOverTime, expirationTime);
        }

        /// <summary>
        /// Helper method to clean up any dictionary that stores message counts.
        /// </summary>
        private static void CleanupDictionary(ConcurrentDictionary<DateTime, long> dictionary, DateTime expirationTime)
        {
            var keysToRemove = dictionary.Keys
                .Where(key => key < expirationTime)
                .ToList();

            foreach (var key in keysToRemove)
            {
                dictionary.TryRemove(key, out _);
            }
        }
    }
}