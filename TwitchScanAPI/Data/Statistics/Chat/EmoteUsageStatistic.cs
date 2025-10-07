using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class EmoteUsageStatistic : StatisticBase
    {
        private readonly ConcurrentDictionary<string, int> _emoteCounts = new(StringComparer.Ordinal);
        private const int MaxTopEmotes = 20;

        public override string Name => "EmoteUsage";

        protected override object ComputeResult()
        {
            // Use a more memory-efficient top-k selection without full sorting
            var topEmotes = new Dictionary<string, int>(MaxTopEmotes);

            foreach (var (emote, count) in _emoteCounts)
            {
                if (topEmotes.Count < MaxTopEmotes)
                {
                    topEmotes[emote] = count;
                }
                else
                {
                    // Find and replace the minimum
                    var minEntry = topEmotes.MinBy(kvp => kvp.Value);
                    if (count > minEntry.Value)
                    {
                        topEmotes.Remove(minEntry.Key);
                        topEmotes[emote] = count;
                    }
                }
            }

            return topEmotes.OrderByDescending(kvp => kvp.Value)
                           .Select(kvp => new KeyValuePair<string, int>(kvp.Key, kvp.Value))
                           .ToList();
        }

        public Task Update(ChannelMessage message)
        {
            var emotes = message.ChatMessage.Emotes;

            // Process emotes with early validation to reduce dictionary operations
            foreach (var emote in emotes)
            {
                if (!string.IsNullOrWhiteSpace(emote.Name))
                {
                    _emoteCounts.AddOrUpdate(emote.Name, 1, (_, count) => count + 1);
                }
            }

            HasUpdated = true;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            _emoteCounts.Clear();
        }
    }
}
