using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch
{
    public class RaidEvent : TimedEntity
    {
        public string Raider { get; set; }
        public int ViewerCount { get; set; }
    }
}