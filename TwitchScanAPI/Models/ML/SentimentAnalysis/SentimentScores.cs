namespace TwitchScanAPI.Models.ML.SentimentAnalysis
{
    public class SentimentScores
    {
        public long MessageCount = 0;
        public double Positive = 0;
        public double Negative = 0;
        public double Neutral = 0;
        public double Compound = 0;
        public readonly object Lock = new();
    }
}