using System;
using System.Collections.Concurrent;
using System.Linq;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch;

namespace TwitchScanAPI.Data.Statistics
{
    public class TopChattersStatistic : IStatistic
    {
        public string Name => "TopChatters";
        private readonly ConcurrentDictionary<string, int> _chatterCounts = new(StringComparer.OrdinalIgnoreCase);
        private readonly int _topN;
        
        public TopChattersStatistic()
        {
            _topN = 10;
        }

        public TopChattersStatistic(int topN = 10)
        {
            _topN = topN;
        }

        public object GetResult()
        {
            return _chatterCounts
                .OrderByDescending(kv => kv.Value)
                .Take(_topN)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public void Update(ChannelMessage message)
        {
            _chatterCounts.AddOrUpdate(message.ChatMessage.Username, 1, (key, oldValue) => oldValue + 1);
        }
    }
}