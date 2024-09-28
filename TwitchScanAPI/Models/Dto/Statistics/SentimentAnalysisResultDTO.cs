using System.Collections.Generic;

namespace TwitchScanAPI.Models.Dto.Statistics
{
    public class SentimentAnalysisResultDTO
    {
        public List<SentimentOverTimeDTO> SentimentOverTime { get; set; }
        public List<UserSentimentDTO> TopPositiveUsers { get; set; }
        public List<UserSentimentDTO> TopNegativeUsers { get; set; }
        public List<object> SentimentOverTimeLabeled { get; set; }
        public List<SentimentMessageDTO> TopPositiveMessages { get; set; }
        public List<SentimentMessageDTO> TopNegativeMessages { get; set; }
    }
}