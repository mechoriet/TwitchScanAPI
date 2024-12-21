namespace TwitchScanAPI.Models.ML.SentimentAnalysis
{
    public class SentimentScores
    {
        public readonly object Lock = new();
        public double Compound = 0;
        public long MessageCount = 0;
        public double Negative = 0;
        public double Neutral = 0;
        public double Positive = 0;
    }
}