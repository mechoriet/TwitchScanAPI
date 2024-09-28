using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch
{
    public class TimedOutUser : TimedEntity
    {
        public string Username { get; set; }
        public string TimeoutReason { get; set; }
        public int TimeoutDuration { get; set; }
    }
}