using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch
{
    public class HostEvent : TimedEntity
    {
        public string Hoster { get; set; }
        public int ViewerCount { get; set; }
    }
}