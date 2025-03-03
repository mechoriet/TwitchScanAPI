using System.Collections.Generic;

namespace TwitchScanAPI.Models.Twitch.Statistics;

public class BanMetrics
{
    public int TotalBans { get; set; }
    public IEnumerable<BanReasonResult> BanReasons { get; set; } = [];
    
}