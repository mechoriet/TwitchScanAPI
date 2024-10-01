using System;

namespace TwitchScanAPI.Models.Dto.Twitch.Channel
{
    public class InitiatedChannel
    {
        public string ChannelName { get; set; }
        public int MessageCount { get; set; }
        public DateTime CreatedAt { get; set; }
        
        public InitiatedChannel(string channelName, int messageCount, DateTime createdAt)
        {
            ChannelName = channelName;
            MessageCount = messageCount;
            CreatedAt = createdAt;
        }
    }
}