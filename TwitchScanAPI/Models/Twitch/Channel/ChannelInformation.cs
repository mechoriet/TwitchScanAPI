using System;

namespace TwitchScanAPI.Models.Twitch.Channel
{
    public class ChannelInformation
    {
        public long Viewers { get; set; }
        public string Title { get; set; }
        public string Game { get; set; }
        public DateTime Uptime { get; set; }
        public string Thumbnail { get; set; }
        public string StreamType { get; set; }
    }
}