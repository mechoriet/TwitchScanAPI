using System.Collections.Generic;
using TwitchScanAPI.Models.Twitch.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Models.Twitch.Statistics
{
    public class BotResult : TimedEntity
    {
        public List<BotLikelinessResult> TopSuspiciousUsers { get; set; }
        public List<Snapshot> RecentSnapshots { get; set; }
    }
}