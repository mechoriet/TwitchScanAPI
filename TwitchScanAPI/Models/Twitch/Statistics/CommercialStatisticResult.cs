using System.Collections.Generic;
using TwitchScanAPI.Models.Twitch.Base;
using TwitchScanAPI.Models.Twitch.Channel;

namespace TwitchScanAPI.Models.Twitch.Statistics;

public class CommercialStatisticResult : TimedEntity
{
    public int TotalCommercialTime { get; set; }
    public Dictionary<string, List<ChannelCommercial>> CommercialsOverTime { get; set; } = new();
}