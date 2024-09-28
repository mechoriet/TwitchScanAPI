using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.Channel
{
    public class ChannelRaid : TimedEntity
    {
        public string Raider { get; set; }
        public int ViewerCount { get; set; }
    }
}