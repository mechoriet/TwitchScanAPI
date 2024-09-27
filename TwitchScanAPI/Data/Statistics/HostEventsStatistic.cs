using System;
using System.Threading;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch;

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
        
        public void Update(HostEvent hostEvent)
        {
            Interlocked.Increment(ref _hostCount);
        }
    }
}