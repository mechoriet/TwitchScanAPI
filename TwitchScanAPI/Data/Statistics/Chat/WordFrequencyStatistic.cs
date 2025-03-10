using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class WordFrequencyStatistic : StatisticBase
    {
        private static readonly Regex WordSplitter = new(@"\W+", RegexOptions.Compiled);
        private ConcurrentDictionary<string, int> _wordCounts = new();

        public override string Name => "WordFrequency";

        protected override object ComputeResult()
        {
            var topWords = new SortedSet<(int count, string word)>();

            foreach (var kv in _wordCounts)
            {
                topWords.Add((kv.Value, kv.Key));
                if (topWords.Count > 10)
                    topWords.Remove(topWords.Min);
            }

            return topWords
                .OrderByDescending(entry => entry.count)
                .ToDictionary(entry => entry.word, entry => entry.count);
        }

        public Task Update(ChannelMessage message)
        {
            var words = WordSplitter.Split(message.ChatMessage.Message);
            foreach (var word in words)
            {
                var trimmed = word.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;
                _wordCounts.AddOrUpdate(trimmed.ToLower(), 1, (_, count) => count + 1);
            }

            HasUpdated = true;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            _wordCounts = new ConcurrentDictionary<string, int>();
        }
    }
}