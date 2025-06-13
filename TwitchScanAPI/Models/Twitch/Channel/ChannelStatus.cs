using System;

namespace TwitchScanAPI.Models.Twitch.Channel
{
    public readonly struct ChannelStatus
    {
        public string ChannelName { get; init; }
        public bool IsOnline { get; init; }
        public long MessageCount { get; init; }
        public long ViewerCount { get; init; }
        public DateTime Uptime { get; init; }

        public ChannelStatus(string channelName, bool isOnline, long messageCount, long viewerCount, DateTime uptime)
        {
            ChannelName = channelName;
            IsOnline = isOnline;
            MessageCount = messageCount;
            ViewerCount = viewerCount;
            Uptime = uptime;
        }
    }
}