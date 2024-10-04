using System.Collections.Generic;
using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.Statistics
{
    public class SentimentAnalysisResult : TimedEntity
    {
        public List<SentimentOverTime> SentimentOverTime { get; set; } = new();
        public List<UserSentiment> TopPositiveUsers { get; set; } = new();
        public List<UserSentiment> TopNegativeUsers { get; set; } = new();
        public List<object> SentimentOverTimeLabeled { get; set; } = new();
        public List<SentimentMessage> TopPositiveMessages { get; set; } = new();
        public List<SentimentMessage> TopNegativeMessages { get; set; } = new();
    }
}