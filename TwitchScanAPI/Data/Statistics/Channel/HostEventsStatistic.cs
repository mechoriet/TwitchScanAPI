using System.Threading;
using TwitchScanAPI.Data.Statistics.Chat.Base;
using TwitchScanAPI.Models.Twitch.Channel;

namespace TwitchScanAPI.Data.Statistics
{
    public class HostEventsStatistic : IStatistic
    {
        public string Name => "HostEvents";
        private int _hostCount = 0;

        public object GetResult()
        {
            return _hostCount;
        }
        
        public void Update(ChannelHost channelHost)
        {
            Interlocked.Increment(ref _hostCount);
        }
    }
}