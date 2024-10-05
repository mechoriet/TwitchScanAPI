using System;
using System.Collections.Generic;
using System.Linq;
using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.Statistics
{
    public class PeakActivityPeriods : TimedEntity
    {
        public Dictionary<string, long> MessagesOverTime { get; private set; } = new();
        public Dictionary<string, long> SubOnlyMessagesOverTime { get; private set; } = new();
        public Dictionary<string, long> EmoteOnlyMessagesOverTime { get; private set; } = new();
        public Dictionary<string, long> SlowModeMessagesOverTime { get; private set; } = new();

        public static PeakActivityPeriods Create(
            IDictionary<DateTime, long> messagesOverTime,
            IDictionary<DateTime, long> subOnlyMessagesOverTime,
            IDictionary<DateTime, long> emoteOnlyMessagesOverTime,
            IDictionary<DateTime, long> slowModeMessagesOverTime)
        {
            return new PeakActivityPeriods
            {
                MessagesOverTime = messagesOverTime
                    .OrderByDescending(kv => kv.Key)
                    .ToDictionary(kv => kv.Key.ToString("yyyy-MM-ddTHH:mm:ssZ"), kv => kv.Value),
                SubOnlyMessagesOverTime = subOnlyMessagesOverTime
                    .OrderByDescending(kv => kv.Key)
                    .ToDictionary(kv => kv.Key.ToString("yyyy-MM-ddTHH:mm:ssZ"), kv => kv.Value),
                EmoteOnlyMessagesOverTime = emoteOnlyMessagesOverTime
                    .OrderByDescending(kv => kv.Key)
                    .ToDictionary(kv => kv.Key.ToString("yyyy-MM-ddTHH:mm:ssZ"), kv => kv.Value),
                SlowModeMessagesOverTime = slowModeMessagesOverTime
                    .OrderByDescending(kv => kv.Key)
                    .ToDictionary(kv => kv.Key.ToString("yyyy-MM-ddTHH:mm:ssZ"), kv => kv.Value),
            };
        }
    }
}
