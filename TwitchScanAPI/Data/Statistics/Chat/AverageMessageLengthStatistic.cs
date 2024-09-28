using System.Threading;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class AverageMessageLengthStatistic : IStatistic
    {
        public string Name => "AverageMessageLength";
        private long _totalLength;
        private long _messageCount;

        public object GetResult()
        {
            if (_messageCount == 0) return 0;
            return (double)_totalLength / _messageCount;
        }

        public void Update(ChannelMessage message)
        {
            Interlocked.Add(ref _totalLength, message.ChatMessage.Message.Length);
            Interlocked.Increment(ref _messageCount);
        }
    }
}