using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Client.Models;
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

        public Task Update(ChannelMessage message)
        {
            var emotes = message.ChatMessage.Emotes;
            if (emotes == null || !emotes.Any()) return Task.CompletedTask;

            foreach (var emote in emotes)
            {
                if (string.IsNullOrWhiteSpace(emote)) continue;
                _emoteCounts.AddOrUpdate(emote, 1, (_, count) => count + 1);
            }
            return Task.CompletedTask;
        }
    }
}