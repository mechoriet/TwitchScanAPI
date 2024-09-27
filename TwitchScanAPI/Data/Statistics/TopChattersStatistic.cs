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

        // Default constructor sets _topN to 10
        public TopChattersStatistic() : this(10) { }

        // Constructor allows for a custom number of top chatters to be tracked
        public TopChattersStatistic(int topN = 10)
        {
            _topN = Math.Max(topN, 1); // Ensure _topN is at least 1
        }

        public object GetResult()
        {
            // Return the top N chatters ordered by message count
            return _chatterCounts
                .OrderByDescending(kv => kv.Value)
                .Take(_topN)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public void Update(ChannelMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.ChatMessage?.Username)) return; // Handle null or empty usernames

            // Add or update the chatter's message count in a thread-safe manner
            _chatterCounts.AddOrUpdate(message.ChatMessage.Username.Trim(), 1, (key, oldValue) => oldValue + 1);
        }
    }

}