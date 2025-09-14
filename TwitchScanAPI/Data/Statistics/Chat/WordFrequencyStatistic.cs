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
            var heap = new PriorityQueue<string, int>();

            foreach (var (word, count) in _wordCounts)
            {
                heap.Enqueue(word, count);

                if (heap.Count > 10)
                    heap.Dequeue(); // remove lowest count
            }

            // Extract and sort descending
            var result = new List<(string word, int count)>();
            while (heap.Count > 0)
            {
                var word = heap.Dequeue();
                result.Add((word, _wordCounts[word]));
            }

            return result
                .OrderByDescending(entry => entry.count)
                .ToDictionary(entry => entry.word, entry => entry.count);
        }

        public Task Update(ChannelMessage message)
        {
            var chatMsg = message.ChatMessage;
            var emoteTexts = new HashSet<string>(chatMsg.Emotes.Select(e => e.Name), StringComparer.OrdinalIgnoreCase);

            var words = WordSplitter.Split(chatMsg.Message);
            foreach (var word in words)
            {
                var trimmed = word.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                var lower = trimmed.ToLowerInvariant();
                if (emoteTexts.Contains(lower))
                    continue; // Skip emotes

                _wordCounts.AddOrUpdate(lower, 1, (_, count) => count + 1);
            }

            HasUpdated = true;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            _wordCounts.Clear();
        }
    }
}