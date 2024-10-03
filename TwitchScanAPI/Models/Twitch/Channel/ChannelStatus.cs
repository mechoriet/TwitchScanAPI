﻿namespace TwitchScanAPI.Models.Twitch.Channel
{
    public class ChannelStatus
    {
        public string ChannelName { get; set; }
        public bool IsOnline { get; set; }
        public long MessageCount { get; set; }
        public long ViewerCount { get; set; }
        
        public ChannelStatus(string channelName, bool isOnline, long messageCount, long viewerCount)
        {
            ChannelName = channelName;
            IsOnline = isOnline;
            MessageCount = messageCount;
            ViewerCount = viewerCount;
        }
    }   
}