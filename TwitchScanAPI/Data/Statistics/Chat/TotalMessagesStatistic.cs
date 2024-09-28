using System.Threading;
using TwitchScanAPI.Data.Statistics.Chat.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class TotalMessagesStatistic : IStatistic
    {
        public string Name => "TotalMessages";
        private int _totalMessages = 0;

        public object GetResult()
        {
            return _totalMessages;
        }

        public void Update(ChannelMessage message)
        {
            Interlocked.Increment(ref _totalMessages);
        }
    }
}