using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.Statistics
{
    public class SentimentOverTime : TimedEntity
    {
        public double AveragePositive { get; set; }
        public double AverageNegative { get; set; }
        public double AverageNeutral { get; set; }
        public double AverageCompound { get; set; }
        public long MessageCount { get; set; }
    }
}