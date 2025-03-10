using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Statistics;
using TwitchScanAPI.Models.Twitch.User;

namespace TwitchScanAPI.Data.Statistics.User
{
    public class TotalTimeoutsStatistic : StatisticBase
    {
        private ConcurrentDictionary<string, int> _timeoutReasons = new(StringComparer.OrdinalIgnoreCase);
        private int _timeoutCount;
        private long _totalTimeoutDuration;
        public override string Name => "TotalTimeouts";

        protected override object ComputeResult()
        {
            return new TimeoutMetrics
            {
                TotalTimeouts = _timeoutCount,
                TotalTimeoutDuration = _totalTimeoutDuration,
                AverageTimeoutDuration = _timeoutCount == 0 ? 0 : (double)_totalTimeoutDuration / _timeoutCount,
                TimeoutReasons = _timeoutReasons.OrderByDescending(kvp => kvp.Value).Select(kvp => new TimeoutReasonResult
                {
                    Reason = kvp.Key,
                    ReasonCount = kvp.Value
                })
            };
        }

        public Task Update(UserTimedOut userTimedOut)
        {
            _timeoutCount++;
            _totalTimeoutDuration += userTimedOut.TimeoutDuration;
            if (!string.IsNullOrWhiteSpace(userTimedOut.TimeoutReason))
                _timeoutReasons.AddOrUpdate(userTimedOut.TimeoutReason.Trim(), 1, (_, count) => count + 1);
            HasUpdated = true;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            _timeoutCount = 0;
            _totalTimeoutDuration = 0;
            _timeoutReasons = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }
}