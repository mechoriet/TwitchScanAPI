using System.Threading;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class AverageMessageLengthStatistic : IStatistic
    {
        private long _messageCount;
        private long _totalLength;
        public string Name => "AverageMessageLength";

        public object GetResult()
        {
            if (_messageCount == 0) return 0;
            return (double)_totalLength / _messageCount;
        }

        public Task Update(ChannelMessage message)
        {
            Interlocked.Add(ref _totalLength, message.ChatMessage.Message.Length);
            Interlocked.Increment(ref _messageCount);
            return Task.CompletedTask;
        }
        
        public void Dispose()
        {
            _messageCount = 0;
            _totalLength = 0;
        }
    }
}