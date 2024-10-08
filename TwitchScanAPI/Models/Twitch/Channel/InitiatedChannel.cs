using System;

namespace TwitchScanAPI.Models.Twitch.Channel
{
    public class InitiatedChannel
    {
        public string ChannelName { get; set; }
        public string Title { get; set; }
        public string Game { get; set; }
        public long MessageCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime StreamingSince { get; set; }
        public bool IsOnline { get; set; }
        
        public InitiatedChannel(string channelName, long messageCount, DateTime createdAt, DateTime streamingSince, bool isOnline, string title, string game)
        {
            ChannelName = channelName;
            MessageCount = messageCount;
            CreatedAt = createdAt;
            StreamingSince = streamingSince;
            IsOnline = isOnline;
            Title = title;
            Game = game;
        }
    }
}