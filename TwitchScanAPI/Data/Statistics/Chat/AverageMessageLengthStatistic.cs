using System.Threading;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class AverageMessageLengthStatistic : StatisticBase
    {
        private long _messageCount;
        private long _totalLength;
        public override string Name => "AverageMessageLength";

        protected override object ComputeResult()
        {
            return _messageCount == 0 ? 0 : (double)_totalLength / _messageCount;
        }

        public Task Update(ChannelMessage message)
        {
            Interlocked.Add(ref _totalLength, message.ChatMessage.Message.Length);
            Interlocked.Increment(ref _messageCount);
            HasUpdated = true;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            _messageCount = 0;
            _totalLength = 0;
        }
    }
}