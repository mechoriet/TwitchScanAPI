using System;

namespace TwitchScanAPI.Models.Twitch.Channel
{
    public class InitiatedChannel
    {
        public string ChannelName { get; set; }
        public long MessageCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsOnline { get; set; }
        public long HistoryLength { get; set; }
        
        public InitiatedChannel(string channelName, long messageCount, DateTime createdAt, bool isOnline, long historyLength)
        {
            ChannelName = channelName;
            MessageCount = messageCount;
            CreatedAt = createdAt;
            IsOnline = isOnline;
            HistoryLength = historyLength;
        }
    }
}