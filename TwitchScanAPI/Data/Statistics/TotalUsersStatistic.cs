using System;
using System.Collections.Concurrent;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch;
using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Data.Statistics
{
    public class TotalUsersStatistic : IStatistic
    {
        public string Name => "TotalUsers";
        private readonly ConcurrentDictionary<string, byte> _users = new(StringComparer.OrdinalIgnoreCase); // Case-insensitive username comparison

        public object GetResult()
        {
            // Return the count of unique users
            return _users.Count;
        }

        public void Update(ChannelMessage message)
        {
            AddUser(message?.ChatMessage?.Username);
        }
        
        public void Update(UserEntity userEntity)
        {
            AddUser(userEntity?.Username);
        }
        
        private void AddUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return; // Handle null or empty usernames

            // Add the username to the dictionary (case-insensitive due to the StringComparer)
            _users.TryAdd(username.Trim(), 0);
        }
    }
}