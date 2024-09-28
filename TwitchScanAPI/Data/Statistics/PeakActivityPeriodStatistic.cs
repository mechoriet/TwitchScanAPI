﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch;

namespace TwitchScanAPI.Data.Statistics
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
                .Take(10)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public void Update(ChannelMessage message)
        {
            if (message?.Time == null) return; // Handle null message or time

            // Get the hour in UTC format as MM/dd/yyyy-HH:00:00
            var dateTime = message.Time.ToUniversalTime();
            // Round the minutes to the nearest 10
            var roundedMinutes = Math.Floor((double)dateTime.Minute / 10) * 10;
            var hour = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, (int)roundedMinutes, 0)
                .ToString("MM/dd/yyyy-HH:mm:00");


            // Add or update the count for the current hour in a thread-safe manner
            _hourlyMessageCounts.AddOrUpdate(hour, 1, (key, oldValue) => oldValue + 1);
        }
    }

}