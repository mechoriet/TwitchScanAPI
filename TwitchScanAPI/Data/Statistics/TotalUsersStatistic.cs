using System;
using System.Collections.Concurrent;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch;

namespace TwitchScanAPI.Data.Statistics
{
    public class TotalUsersStatistic : IStatistic
    {
        public string Name => "TotalUsers";
        private readonly ConcurrentDictionary<string, byte> _users = new(StringComparer.OrdinalIgnoreCase);

        public object GetResult()
        {
            return _users.Count;
        }

        public void Update(ChannelMessage message)
        {
            _users.TryAdd(message.ChatMessage.Username, 0);
        }
    }
}