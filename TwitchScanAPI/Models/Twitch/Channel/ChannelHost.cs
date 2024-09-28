using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.Channel
{
    public class ChannelHost : TimedEntity
    {
        public string Hoster { get; set; }
        public int ViewerCount { get; set; }
    }
}