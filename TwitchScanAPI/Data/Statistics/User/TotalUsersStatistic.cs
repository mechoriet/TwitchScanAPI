using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.User
{
    public class TotalUsersStatistic : IStatistic
    {
        private ConcurrentDictionary<string, byte>
            _users = new(StringComparer.OrdinalIgnoreCase); // Case-insensitive username comparison

        public string Name => "TotalUsers";

        public object GetResult()
        {
            // Return the count of unique users
            return _users.Count;
        }

        public Task Update(ChannelMessage message)
        {
            AddUser(message.ChatMessage.Username);
            return Task.CompletedTask;
        }

        public Task Update(UserEntity userEntity)
        {
            AddUser(userEntity.Username);
            return Task.CompletedTask;
        }

        private void AddUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return; // Handle null or empty usernames

            // Add the username to the dictionary (case-insensitive due to the StringComparer)
            _users.TryAdd(username.Trim(), 0);
        }
        
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _users = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        }
    }
}