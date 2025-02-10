using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class EmoteUsageStatistic : IStatistic
    {
        private readonly ConcurrentDictionary<string, int> _emoteCounts = new(StringComparer.OrdinalIgnoreCase);
        public string Name => "EmoteUsage";

        public object GetResult()
        {
            var topEmotes = _emoteCounts.OrderByDescending(kvp => kvp.Value).Take(20).ToList();
            return topEmotes;
        }

        public Task Update(ChannelMessage message)
        {
            var emotes = message.ChatMessage.Emotes;

            foreach (var emote in emotes.Where(emote => !string.IsNullOrWhiteSpace(emote.Name)))
                _emoteCounts.AddOrUpdate(emote.Name, 1, (_, count) => count + 1);
            return Task.CompletedTask;
        }
        
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _emoteCounts.Clear();
        }
    }
}