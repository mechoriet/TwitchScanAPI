using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch;

namespace TwitchScanAPI.Data.Statistics
{
    public class EmoteUsageStatistic : IStatistic
    {
        public string Name => "EmoteUsage";
        private readonly ConcurrentDictionary<string, int> _emoteCounts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Regex _emoteRegex = new(@"\b(Kappa|PogChamp|FeelsBadMan|LUL)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public object GetResult()
        {
            return _emoteCounts.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public void Update(ChannelMessage message)
        {
            var matches = _emoteRegex.Matches(message.ChatMessage.Message);
            foreach (Match match in matches)
            {
                var emote = match.Value;
                _emoteCounts.AddOrUpdate(emote, 1, (key, oldValue) => oldValue + 1);
            }
        }
    }
}