using System.Collections.Generic;

namespace TwitchScanAPI.Models.Twitch.Statistics
{
    public class RaidStatisticResult
    {
        public int TotalRaids { get; set; }
        public Dictionary<string, int> TopRaiders { get; set; } = new();
        public List<KeyValuePair<string, string>> RaidsOverTime { get; set; } = new();
    }
}