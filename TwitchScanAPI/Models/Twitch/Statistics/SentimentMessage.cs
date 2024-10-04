using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.Statistics
{
    public class SentimentMessage : TimedEntity
    {
        public string Username { get; set; }
        public string Message { get; set; }
        public double Positive { get; set; }
        public double Negative { get; set; }
        public double Neutral { get; set; }
        public double Compound { get; set; }
    }
}