using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class SentenceFrequencyStatistic : IStatistic
    {
        public string Name => "SentenceFrequency";
        private readonly ConcurrentDictionary<string, int> _sentenceCounts = new();

        private static readonly Regex SentenceSplitter = new(@"\.|\?|!|\n", RegexOptions.Compiled);

        public object GetResult()
        {
            // Optimized result collection using a min-heap to track the top 10 items
            var topSentences = new SortedSet<(int count, string sentence)>();

            foreach (var kv in _sentenceCounts)
            {
                topSentences.Add((kv.Value, kv.Key));

                // Only keep top 10
                if (topSentences.Count > 10)
                {
                    topSentences.Remove(topSentences.Min); // Remove smallest if over capacity
                }
            }

            // Convert result to dictionary
            return topSentences
                .OrderByDescending(entry => entry.count)
                .ToDictionary(entry => entry.sentence, entry => entry.count);
        }

        public Task Update(ChannelMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.ChatMessage.Message)) 
                return Task.CompletedTask; // Handle empty messages

            // Check if the message has any punctuation at all
            if (!Regex.IsMatch(message.ChatMessage.Message, @"[.!?\n]"))
            {
                // No punctuation found, treat the whole message as a single sentence
                var trimmedMessage = message.ChatMessage.Message.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedMessage))
                {
                    _sentenceCounts.AddOrUpdate(trimmedMessage.ToLower(), 1, (_, count) => count + 1);
                }
                return Task.CompletedTask;
            }

            // Otherwise, split the message using the SentenceSplitter regex
            var sentences = SentenceSplitter.Split(message.ChatMessage.Message);
            foreach (var sentence in sentences)
            {
                var trimmed = sentence.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue; // Skip empty sentences

                _sentenceCounts.AddOrUpdate(trimmed.ToLower(), 1, (_, count) => count + 1);
            }
            return Task.CompletedTask;
        }

    }
}