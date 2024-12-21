using System.Collections.Generic;
using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.Statistics
{
    public class SubscriptionStatisticResult : TimedEntity
    {
        // Total number of subscribers
        public int TotalSubscribers { get; set; }

        // Number of new subscribers
        public int TotalNewSubscribers { get; set; }

        // Number of returning subscribers
        public int TotalReSubscribers { get; set; }

        // Number of gifted subscriptions
        public int TotalGiftedSubscriptions { get; set; }

        // Number of community subscriptions
        public int TotalCommunitySubscriptions { get; set; }

        // Average subscription months
        public double AverageSubscriptionMonths { get; set; }

        // Top subscribers with the total number of months subscribed
        public Dictionary<string, int> TopSubscribers { get; set; } = new();

        // Subscriptions aggregated over time, grouped by the timestamp
        public List<KeyValuePair<string, long>> SubscriptionsOverTime { get; set; } = new();

        // Trend of the subscription count
        public Trend Trend { get; set; }
    }
}