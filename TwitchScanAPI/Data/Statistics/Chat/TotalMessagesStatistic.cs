using System.Threading;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class TotalMessagesStatistic : IStatistic
    {
        public string Name => "TotalMessages";
        private int _totalMessages;

        public object GetResult()
        {
            return _totalMessages;
        }

        public Task Update(ChannelMessage message)
        {
            Interlocked.Increment(ref _totalMessages);
            return Task.CompletedTask;
        }
    }
}