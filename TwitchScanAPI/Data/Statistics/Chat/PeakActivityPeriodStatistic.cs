using System;
using System.Collections.Concurrent;
using System.Linq;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class PeakActivityPeriodStatistic : IStatistic
    {
        public string Name => "PeakActivityPeriods";
        private readonly ConcurrentDictionary<string, int> _hourlyMessageCounts = new();

        public object GetResult()
        {
            // Return the top 3 hours with the highest message counts
            return _hourlyMessageCounts
                .OrderByDescending(kv => kv.Value)
                .Take(100)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public void Update(ChannelMessage message)
        {
            if (message?.Time == null) return; // Handle null message or time

            // Get the time in UTC format
            var dateTime = message.Time.ToUniversalTime();
    
            // Round the minutes to the nearest 5-minute period
            var roundedMinutes = Math.Floor((double)dateTime.Minute / 5) * 5;

            // Create a new DateTime with the rounded minutes
            var roundedTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, (int)roundedMinutes, 0)
                .ToString("yyyy-MM-ddTHH:mm:ss");

            // Add or update the count for the current rounded time in a thread-safe manner
            _hourlyMessageCounts.AddOrUpdate(roundedTime, 1, (key, oldValue) => oldValue + 1);
        }
    }

}