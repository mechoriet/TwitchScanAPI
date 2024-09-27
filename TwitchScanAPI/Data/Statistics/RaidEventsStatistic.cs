using System.Threading;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch;

namespace TwitchScanAPI.Data.Statistics
{
    public class RaidEventsStatistic : IStatistic
    {
        public string Name => "RaidEvents";
        private int _raidCount = 0;

        public object GetResult()
        {
            return _raidCount;
        }
        
        public void Update(RaidEvent raidEvent)
        {
            Interlocked.Increment(ref _raidCount);
        }
    }
}