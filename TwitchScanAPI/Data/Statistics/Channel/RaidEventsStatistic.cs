using System.Threading;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch;
using TwitchScanAPI.Models.Twitch.Channel;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class RaidEventsStatistic : IStatistic
    {
        public string Name => "RaidEvents";
        private int _raidCount;

        public object GetResult()
        {
            return _raidCount;
        }
        
        public void Update(ChannelRaid channelRaid)
        {
            Interlocked.Increment(ref _raidCount);
        }
    }
}