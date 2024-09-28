using System;
using System.Collections.Concurrent;
using System.Linq;
using TwitchScanAPI.Data.Statistics.Chat.Base;
using TwitchScanAPI.Models.Twitch.User;

namespace TwitchScanAPI.Data.Statistics.User
{
    public class TotalTimeoutsStatistic : IStatistic
    {
        public string Name => "TotalTimeouts";

        private int _timeoutCount;
        private long _totalTimeoutDuration;
        private readonly ConcurrentDictionary<string, int> _timeoutReasons = new(StringComparer.OrdinalIgnoreCase);

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

        public void Update(UserTimedOut userTimedOut)
        {
            if (userTimedOut == null) return;

            _timeoutCount++;
            _totalTimeoutDuration += userTimedOut.TimeoutDuration;

            if (!string.IsNullOrWhiteSpace(userTimedOut.TimeoutReason))
            {
                _timeoutReasons.AddOrUpdate(userTimedOut.TimeoutReason.Trim(), 1, (key, count) => count + 1);
            }
        }
    }

}