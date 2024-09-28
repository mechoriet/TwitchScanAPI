using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.User
{
    public class UserTimedOut : TimedEntity
    {
        public string Username { get; set; }
        public string TimeoutReason { get; set; }
        public int TimeoutDuration { get; set; }
    }
}