using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.User
{
    public class TotalUsersStatistic : StatisticBase
    {
        private ConcurrentDictionary<string, byte> _users = new(StringComparer.OrdinalIgnoreCase);

        public override string Name => "TotalUsers";

        protected override object ComputeResult()
        {
            return _users.Count;
        }

        public Task Update(ChannelMessage message)
        {
            AddUser(message.ChatMessage.Username);
            HasUpdated = true;
            return Task.CompletedTask;
        }

        public Task Update(UserEntity userEntity)
        {
            AddUser(userEntity.Username);
            HasUpdated = true;
            return Task.CompletedTask;
        }

        private void AddUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return;
            _users.TryAdd(username.Trim(), 0);
        }

        public override void Dispose()
        {
            base.Dispose();
            _users = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        }
    }
}