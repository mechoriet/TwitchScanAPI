using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class EmoteUsageStatistic : StatisticBase
    {
        private ConcurrentDictionary<string, int> _emoteCounts = new(StringComparer.OrdinalIgnoreCase);
        public override string Name => "EmoteUsage";

        protected override object ComputeResult()
        {
            var topEmotes = _emoteCounts.OrderByDescending(kvp => kvp.Value).Take(20).ToList();
            return topEmotes;
        }

        public Task Update(ChannelMessage message)
        {
            var emotes = message.ChatMessage.Emotes;

            foreach (var emote in emotes.Where(e => !string.IsNullOrWhiteSpace(e.Name)))
                _emoteCounts.AddOrUpdate(emote.Name, 1, (_, count) => count + 1);

            HasUpdated = true;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            _emoteCounts = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }
}