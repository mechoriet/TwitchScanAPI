using System.Collections.Concurrent;
using System.Linq;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch;

namespace TwitchScanAPI.Data.Statistics
{
    public class WordFrequencyStatistic : IStatistic
    {
        public string Name => "WordFrequency";
        private readonly ConcurrentDictionary<string, int> _wordCounts = new();

        public object GetResult()
        {
            return _wordCounts.OrderByDescending(kv => kv.Value).Take(10).ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public void Update(ChannelMessage message)
        {
            var words = message.ChatMessage.Message.Split(' ');
            foreach (var word in words)
            {
                _wordCounts.AddOrUpdate(word.ToLower(), 1, (key, count) => count + 1);
            }
        }
    }
}