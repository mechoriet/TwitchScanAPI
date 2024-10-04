using System.Collections.Generic;

namespace TwitchScanAPI.Models.Dto.Statistics
{
    public class SentimentAnalysisResultDto
    {
        public List<SentimentOverTimeDto> SentimentOverTime { get; set; } = new();
        public List<UserSentimentDto> TopPositiveUsers { get; set; } = new();
        public List<UserSentimentDto> TopNegativeUsers { get; set; } = new();
        public List<object> SentimentOverTimeLabeled { get; set; } = new();
        public List<SentimentMessageDto> TopPositiveMessages { get; set; } = new();
        public List<SentimentMessageDto> TopNegativeMessages { get; set; } = new();
    }
}