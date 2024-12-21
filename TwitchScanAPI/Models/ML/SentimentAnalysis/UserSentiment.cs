using System;

namespace TwitchScanAPI.Models.ML.SentimentAnalysis
{
    public class UserSentiment
    {
        public readonly object Lock = new();
        public double Compound;
        public DateTime LastUpdated = DateTime.UtcNow;
        public long MessageCount;
        public double Negative;
        public double Neutral;
        public double Positive;

        public UserSentiment(string username)
        {
            Username = username;
        }

        public string Username { get; }

        public double AveragePositive => MessageCount > 0 ? Positive / MessageCount : 0;
        public double AverageNegative => MessageCount > 0 ? Negative / MessageCount : 0;
        public double AverageNeutral => MessageCount > 0 ? Neutral / MessageCount : 0;
        public double AverageCompound => MessageCount > 0 ? Compound / MessageCount : 0;
    }
}