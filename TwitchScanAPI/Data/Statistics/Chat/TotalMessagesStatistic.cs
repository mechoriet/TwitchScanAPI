using System.Threading;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class TotalMessagesStatistic : StatisticBase
    {
        private int _totalMessages;
        public override string Name => "TotalMessages";

        protected override object ComputeResult()
        {
            return _totalMessages;
        }

        public Task Update(ChannelMessage message)
        {
            Interlocked.Increment(ref _totalMessages);
            HasUpdated = true;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            _totalMessages = 0;
        }
    }
}