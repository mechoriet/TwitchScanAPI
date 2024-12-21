using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.User;

namespace TwitchScanAPI.Data.Statistics.User
{
    public class TotalTimeoutsStatistic : IStatistic
    {
        private readonly ConcurrentDictionary<string, int> _timeoutReasons = new(StringComparer.OrdinalIgnoreCase);

        private int _timeoutCount;
        private long _totalTimeoutDuration;
        public string Name => "TotalTimeouts";

        public object GetResult()
        {
            return new
            {
                TotalTimeouts = _timeoutCount,
                TotalTimeoutDuration = _totalTimeoutDuration,
                AverageTimeoutDuration = _timeoutCount == 0 ? 0 : (double)_totalTimeoutDuration / _timeoutCount,
                TimeoutReasons = _timeoutReasons.OrderByDescending(kvp => kvp.Value).ToList()
            };
        }

        public Task Update(UserTimedOut userTimedOut)
        {
            _timeoutCount++;
            _totalTimeoutDuration += userTimedOut.TimeoutDuration;

            if (!string.IsNullOrWhiteSpace(userTimedOut.TimeoutReason))
                _timeoutReasons.AddOrUpdate(userTimedOut.TimeoutReason.Trim(), 1, (_, count) => count + 1);
            return Task.CompletedTask;
        }
    }
}