using System.Collections.Concurrent;
using System.Linq;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch;

namespace TwitchScanAPI.Data.Statistics
{
    public class PeakActivityPeriodStatistic : IStatistic
    {
        public string Name => "PeakActivityPeriods";
        private readonly ConcurrentDictionary<string, int> _hourlyMessageCounts = new();

        public object GetResult()
        {
            // Returns the top 3 hours with the highest message counts
            return _hourlyMessageCounts
                .OrderByDescending(kv => kv.Value)
                .Take(3)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public void Update(ChannelMessage message)
        {
            var hour = message.Time.ToUniversalTime().ToString("MM/dd/yyyy-HH:00:00"); // UTC hour
            _hourlyMessageCounts.AddOrUpdate(hour, 1, (key, oldValue) => oldValue + 1);
        }
    }
}