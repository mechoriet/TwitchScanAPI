using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class WordFrequencyStatistic : IStatistic
    {
        public string Name => "WordFrequency";
        private readonly ConcurrentDictionary<string, int> _wordCounts = new();

        private static readonly Regex WordSplitter = new(@"\W+", RegexOptions.Compiled); // Splits by any non-word character

        public object GetResult()
        {
            // Optimized result collection using a min-heap to track the top 10 items
            var topWords = new SortedSet<(int count, string word)>();

            foreach (var kv in _wordCounts)
            {
                topWords.Add((kv.Value, kv.Key));

                // Only keep top 10
                if (topWords.Count > 10)
                {
                    topWords.Remove(topWords.Min); // Remove smallest if over capacity
                }
            }

            // Convert result to dictionary
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
                if (string.IsNullOrWhiteSpace(trimmed)) continue; // Skip empty or invalid words

                _wordCounts.AddOrUpdate(trimmed.ToLower(), 1, (_, count) => count + 1);
            }
            return Task.CompletedTask;
        }
    }

}