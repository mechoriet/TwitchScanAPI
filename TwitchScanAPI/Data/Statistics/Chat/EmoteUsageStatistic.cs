using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class EmoteUsageStatistic : IStatistic
    {
        public string Name => "EmoteUsage";

        private readonly ConcurrentDictionary<string, int> _emoteCounts = new(StringComparer.OrdinalIgnoreCase);

        public object GetResult()
        {
            var topEmotes = _emoteCounts.OrderByDescending(kvp => kvp.Value).Take(20).ToList();
            return topEmotes;
        }

        public void Update(ChannelMessage message)
        {
            var emotes = message?.ChatMessage?.EmoteSet.Emotes;
            if (emotes == null || !emotes.Any()) return;

            foreach (var emote in emotes)
            {
                _emoteCounts.AddOrUpdate(emote.Name, 1, (key, count) => count + 1);
            }
        }
    }
}