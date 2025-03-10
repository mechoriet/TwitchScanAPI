using System.Collections.Generic;
using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.Statistics;

public class BanMetrics : TimedEntity
{
    public int TotalBans { get; set; }
    public IEnumerable<BanReasonResult> BanReasons { get; set; } = [];
    
}