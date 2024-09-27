using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch;

namespace TwitchScanAPI.Data.Statistics
{
    public class UniqueWordsStatistic : IStatistic
    {
        public string Name => "UniqueWords";
        private readonly ConcurrentDictionary<string, byte> _uniqueWords = new();

        private static readonly Regex WordRegex = new(@"\b\w+\b", RegexOptions.Compiled);

        public object GetResult()
        {
            return _uniqueWords.Count;
        }

        public void Update(ChannelMessage message)
        {
            var words = WordRegex.Matches(message.ChatMessage.Message)
                .Select(m => m.Value.ToLowerInvariant());

            foreach (var word in words)
            {
                _uniqueWords.TryAdd(word, 0);
            }
        }
    }
}