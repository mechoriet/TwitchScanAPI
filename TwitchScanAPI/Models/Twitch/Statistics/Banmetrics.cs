using System.Collections.Generic;

namespace TwitchScanAPI.Models.Twitch.Statistics;

public class Banmetrics
{
    public int TotalBans { get; set; }
    public IEnumerable<BanReasonResult> BanReasons { get; set; } = [];
    
}