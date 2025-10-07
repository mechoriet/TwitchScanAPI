using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class UniqueWordsStatistic : StatisticBase
    {
        private static readonly Regex WordRegex = new(@"\b\w+\b", RegexOptions.Compiled);
        private readonly ConcurrentDictionary<string, byte> _uniqueWords = new(StringComparer.Ordinal);

        public override string Name => "UniqueWords";

        protected override object ComputeResult()
        {
            return _uniqueWords.Count;
        }

        public Task Update(ChannelMessage message)
        {
            var matches = WordRegex.Matches(message.ChatMessage.Message);

            // Process matches more efficiently by avoiding LINQ allocations
            foreach (Match match in matches)
            {
                var word = match.Value.ToLowerInvariant().Trim();
                if (!string.IsNullOrWhiteSpace(word))
                {
                    _uniqueWords.TryAdd(word, 0);
                }
            }

            HasUpdated = true;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            _uniqueWords.Clear();
        }
    }
}
