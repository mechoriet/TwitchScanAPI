using System;
using System.Collections.Concurrent;
using System.Linq;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch;

namespace TwitchScanAPI.Data.Statistics
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

        public void Update(BannedUser bannedUser)
        {
            if (bannedUser == null) return;

            _banCount++;

            if (!string.IsNullOrWhiteSpace(bannedUser.BanReason))
            {
                _banReasons.AddOrUpdate(bannedUser.BanReason.Trim(), 1, (key, count) => count + 1);
            }
        }
    }
}