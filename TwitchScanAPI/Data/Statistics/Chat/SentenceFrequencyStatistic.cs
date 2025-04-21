using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class SentenceFrequencyStatistic : StatisticBase
    {
        private static readonly Regex SentenceSplitter = new(@"\.|\?|!|\n", RegexOptions.Compiled);
        private readonly ConcurrentDictionary<string, int> _sentenceCounts = new();

        public override string Name => "SentenceFrequency";

        protected override object ComputeResult()
        {
            var heap = new PriorityQueue<string, int>();

            foreach (var (word, count) in _sentenceCounts)
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
                result.Add((word, _sentenceCounts[word]));
            }

            return result
                .OrderByDescending(entry => entry.count)
                .ToDictionary(entry => entry.word, entry => entry.count);
        }

        public Task Update(ChannelMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.ChatMessage.Message))
                return Task.CompletedTask;

            if (!Regex.IsMatch(message.ChatMessage.Message, @"[.!?\n]"))
            {
                var trimmedMessage = message.ChatMessage.Message.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedMessage))
                    _sentenceCounts.AddOrUpdate(trimmedMessage.ToLower(), 1, (_, count) => count + 1);
                HasUpdated = true;
                return Task.CompletedTask;
            }

            var sentences = SentenceSplitter.Split(message.ChatMessage.Message);
            foreach (var sentence in sentences)
            {
                var trimmed = sentence.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;
                _sentenceCounts.AddOrUpdate(trimmed.ToLower(), 1, (_, count) => count + 1);
            }

            HasUpdated = true;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            _sentenceCounts.Clear();
        }
    }
}