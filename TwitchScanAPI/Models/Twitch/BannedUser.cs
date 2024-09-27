using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch
{
    public class BannedUser : TimedEntity
    {
        public string Username { get; set; }
        public string BanReason { get; set; }
    }
}