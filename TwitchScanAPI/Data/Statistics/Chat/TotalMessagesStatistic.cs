using System;
using System.Threading;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class TotalMessagesStatistic : IStatistic
    {
        private int _totalMessages;
        public string Name => "TotalMessages";

        public object GetResult()
        {
            return _totalMessages;
        }

        public Task Update(ChannelMessage message)
        {
            Interlocked.Increment(ref _totalMessages);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _totalMessages = 0;
        }
    }
}