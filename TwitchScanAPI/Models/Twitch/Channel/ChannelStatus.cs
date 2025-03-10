using System;

namespace TwitchScanAPI.Models.Twitch.Channel
{
    public class ChannelStatus(string channelName, bool isOnline, long messageCount, long viewerCount, DateTime uptime)
    {
        public string ChannelName { get; set; } = channelName;
        public bool IsOnline { get; set; } = isOnline;
        public long MessageCount { get; set; } = messageCount;
        public long ViewerCount { get; set; } = viewerCount;
        public DateTime Uptime { get; set; } = uptime;
    }
}