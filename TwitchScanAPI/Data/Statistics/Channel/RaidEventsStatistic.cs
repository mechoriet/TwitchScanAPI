using System.Threading;
using TwitchScanAPI.Data.Statistics.Chat.Base;
using TwitchScanAPI.Models.Twitch;
using TwitchScanAPI.Models.Twitch.Channel;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class RaidEventsStatistic : IStatistic
    {
        public string Name => "RaidEvents";
        private int _raidCount = 0;

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