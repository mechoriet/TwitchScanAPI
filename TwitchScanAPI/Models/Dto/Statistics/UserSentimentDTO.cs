namespace TwitchScanAPI.Models.Dto.Statistics
{
    public class UserSentimentDto
    {
        public string Username { get; set; }
        public double AveragePositive { get; set; }
        public double AverageNegative { get; set; }
        public double AverageNeutral { get; set; }
        public double AverageCompound { get; set; }
        public long MessageCount { get; set; }
    }
}