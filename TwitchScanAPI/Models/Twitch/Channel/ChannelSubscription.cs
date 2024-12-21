using TwitchScanAPI.Models.Enums;
using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.Channel
{
    public class ChannelSubscription : TimedEntity
    {
        public ChannelSubscription(SubscriptionType type)
        {
            Type = type;
        }

        public SubscriptionType Type { get; set; }
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        public int Months { get; set; }
        public int MultiMonth { get; set; }
        public string Message { get; set; }
        public string SubscriptionPlanName { get; set; }
        public string SubscriptionPlan { get; set; }
        public string RecipientUserName { get; set; }
        public string RecipientDisplayName { get; set; }
        public int GiftedSubscriptionCount { get; set; }
        public string GiftedSubscriptionPlan { get; set; }
    }
}