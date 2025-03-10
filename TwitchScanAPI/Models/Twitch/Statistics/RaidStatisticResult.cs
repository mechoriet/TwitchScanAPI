using System.Collections.Generic;
using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.Statistics
{
    public class RaidStatisticResult : TimedEntity
    {
        public int TotalRaids { get; set; }
        public Dictionary<string, int> TopRaiders { get; set; } = new();
        public List<KeyValuePair<string, string>> RaidsOverTime { get; set; } = [];
    }
}