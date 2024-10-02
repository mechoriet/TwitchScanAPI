using System;

namespace TwitchScanAPI.Models.Dto.Twitch.Channel
{
    public class InitiatedChannel
    {
        public string ChannelName { get; set; }
        public int MessageCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsOnline { get; set; }
        
        public InitiatedChannel(string channelName, int messageCount, DateTime createdAt, bool isOnline)
        {
            ChannelName = channelName;
            MessageCount = messageCount;
            CreatedAt = createdAt;
            IsOnline = isOnline;
        }
    }
}