using System;

namespace TwitchScanAPI.Models.Twitch.Statistics
{
    public class StatisticTimeline
    {
        public string Id { get; set; }
        public DateTime Time { get; set; }
        public long PeakViewers { get; set; }
        public long AverageViewers { get; set; }
    }
}