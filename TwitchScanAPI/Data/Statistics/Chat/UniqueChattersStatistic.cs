using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class UniqueChattersStatistic : StatisticBase
    {
        private ConcurrentDictionary<string, byte> _uniqueChatters = new();
        public override string Name => "UniqueChatters";

        protected override object ComputeResult()
        {
            return _uniqueChatters.Count;
        }

        public Task Update(ChannelMessage message)
        {
            var username = message.ChatMessage.Username.ToLowerInvariant().Trim();
            _uniqueChatters.TryAdd(username, 0);
            HasUpdated = true;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            _uniqueChatters = new ConcurrentDictionary<string, byte>();
        }
    }
}