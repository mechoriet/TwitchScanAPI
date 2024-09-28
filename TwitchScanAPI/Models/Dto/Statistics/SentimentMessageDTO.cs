using System;

namespace TwitchScanAPI.Models.Dto.Statistics
{
    public class SentimentMessageDTO
    {
        public string Username { get; set; }
        public string Message { get; set; }
        public double Positive { get; set; }
        public double Negative { get; set; }
        public double Neutral { get; set; }
        public double Compound { get; set; }
        public DateTime Time { get; set; }
    }
}