using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.Statistics
{
    public class PeakActivityPeriods : TimedEntity
    {
        public Trend Trend { get; private set; }
        public Dictionary<string, long> MessagesOverTime { get; private set; } = new();
        public Dictionary<string, long> BitsOverTime { get; private set; } = new();
        public Dictionary<string, long> SubOnlyMessagesOverTime { get; private set; } = new();
        public Dictionary<string, long> EmoteOnlyMessagesOverTime { get; private set; } = new();
        public Dictionary<string, long> SlowModeMessagesOverTime { get; private set; } = new();

        public static PeakActivityPeriods Create(
            Trend trend,
            ConcurrentDictionary<DateTime, long> messagesOverTime,
            ConcurrentDictionary<DateTime, long> bitsOverTime,
            ConcurrentDictionary<DateTime, long> subOnlyMessagesOverTime,
            ConcurrentDictionary<DateTime, long> emoteOnlyMessagesOverTime,
            ConcurrentDictionary<DateTime, long> slowModeMessagesOverTime)
        {
            return new PeakActivityPeriods
            {
                Trend = trend,
                MessagesOverTime = FormatAndSortDictionary(messagesOverTime),
                BitsOverTime = FormatAndSortDictionary(bitsOverTime),
                SubOnlyMessagesOverTime = FormatAndSortDictionary(subOnlyMessagesOverTime),
                EmoteOnlyMessagesOverTime = FormatAndSortDictionary(emoteOnlyMessagesOverTime),
                SlowModeMessagesOverTime = FormatAndSortDictionary(slowModeMessagesOverTime)
            };
        }
        
        private static Dictionary<string, long> FormatAndSortDictionary(IDictionary<DateTime, long> source)
        {
            return source
                .OrderByDescending(kv => kv.Key)
                .ToDictionary(kv => kv.Key.ToString("yyyy-MM-ddTHH:mm:ssZ"), kv => kv.Value);
        }
    }
}