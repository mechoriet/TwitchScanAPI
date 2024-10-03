using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.Statistics
{
    public class StatisticHistory : TimedEntity
    {
        public long PeakViewers { get; set; }
        public long TotalMessages { get; set; }
        public object? Statistics { get; set; }
        
        public StatisticHistory(long peakViewers, long totalMessages, object statistics)
        {
            PeakViewers = peakViewers;
            TotalMessages = totalMessages;
            Statistics = statistics;
        }
        
        public StatisticHistory()
        {
            Statistics = null;
        }
    }
}