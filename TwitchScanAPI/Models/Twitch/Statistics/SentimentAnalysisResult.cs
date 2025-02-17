using System.Collections.Generic;
using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.Statistics
{
    public class SentimentAnalysisResult : TimedEntity
    {
        public IEnumerable<SentimentOverTime> SentimentOverTime { get; set; } = [];
        public IEnumerable<UserSentiment> TopUsers { get; set; } = [];
        public IEnumerable<UserSentiment> TopPositiveUsers { get; set; } = [];
        public IEnumerable<UserSentiment> TopNegativeUsers { get; set; } = [];
        public IEnumerable<SentimentMessage> TopPositiveMessages { get; set; } = [];
        public IEnumerable<SentimentMessage> TopNegativeMessages { get; set; } = [];
        public Trend Trend { get; set; }
    }
}