using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Statistics;
using TwitchScanAPI.Models.Twitch.User;

namespace TwitchScanAPI.Data.Statistics.User
{
    public class TotalBansStatistic : IStatistic
    {
        private ConcurrentDictionary<string, int> _banReasons = new(StringComparer.OrdinalIgnoreCase);

        private int _banCount;
        public string Name => "TotalBans";

        public object GetResult()
        {
            return new BanMetrics
            {
                TotalBans = _banCount,
                BanReasons = _banReasons.OrderByDescending(kvp => kvp.Value).Select(kvp => new BanReasonResult
                {
                    Reason = kvp.Key,
                    ReasonCount = kvp.Value
                })
            };
        }

        public Task Update(UserBanned userBanned)
        {
            _banCount++;

            if (!string.IsNullOrWhiteSpace(userBanned.BanReason))
                _banReasons.AddOrUpdate(userBanned.BanReason.Trim(), 1, (_, count) => count + 1);
            return Task.CompletedTask;
        }
        
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _banCount = 0;
            _banReasons = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }
}