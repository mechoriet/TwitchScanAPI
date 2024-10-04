using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.Statistics
{
    public class StatisticHistory : TimedEntity
    {
        public string UserName { get; set; }
        public long PeakViewers { get; set; }
        public long AverageViewers { get; set; }
        public long TotalMessages { get; set; }
        public object? Statistics { get; set; }
        
        public StatisticHistory(string userName, long peakViewers, long averageViewers, long totalMessages, object statistics)
        {
            UserName = userName;
            PeakViewers = peakViewers;
            AverageViewers = averageViewers;
            TotalMessages = totalMessages;
            Statistics = statistics;
        }
        
        public StatisticHistory(string userName)
        {
            UserName = userName;
            Statistics = null;
        }
    }
}