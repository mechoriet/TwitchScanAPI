using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class UniqueWordsStatistic : IStatistic
    {
        private static readonly Regex WordRegex = new(@"\b\w+\b", RegexOptions.Compiled); // Matches individual words
        private ConcurrentDictionary<string, byte> _uniqueWords = new();
        public string Name => "UniqueWords";

        public object GetResult()
        {
            // Return the count of unique words
            return _uniqueWords.Count;
        }

        public Task Update(ChannelMessage message)
        {
            // Extract words using regex, converting to lower case for consistent comparison
            var words = WordRegex.Matches(message.ChatMessage.Message)
                .Select(m => m.Value.ToLowerInvariant().Trim())
                .Where(w => !string.IsNullOrWhiteSpace(w)); // Filter out empty/whitespace-only matches

            // Add each unique word to the dictionary, if not already present
            foreach (var word in words) _uniqueWords.TryAdd(word, 0);
            return Task.CompletedTask;
        }
        
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _uniqueWords = new ConcurrentDictionary<string, byte>();
        }
    }
}