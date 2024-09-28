using System.Threading;
using TwitchScanAPI.Data.Statistics.Chat.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class AverageMessageLengthStatistic : IStatistic
    {
        public string Name => "AverageMessageLength";
        private long _totalLength = 0;
        private long _messageCount = 0;

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