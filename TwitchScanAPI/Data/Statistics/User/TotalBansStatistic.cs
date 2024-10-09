using System;
using System.Collections.Concurrent;
using System.Linq;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.User;

namespace TwitchScanAPI.Data.Statistics.User
{
    public class TotalBansStatistic : IStatistic
    {
        public string Name => "TotalBans";

        private int _banCount;
        private readonly ConcurrentDictionary<string, int> _banReasons = new(StringComparer.OrdinalIgnoreCase);

        public object GetResult()
        {
            return new
            {
                TotalBans = _banCount,
                BanReasons = _banReasons.OrderByDescending(kvp => kvp.Value).ToList()
            };
        }

        public void Update(UserBanned userBanned)
        {
            _banCount++;

            if (!string.IsNullOrWhiteSpace(userBanned.BanReason))
            {
                _banReasons.AddOrUpdate(userBanned.BanReason.Trim(), 1, (_, count) => count + 1);
            }
        }
    }
}