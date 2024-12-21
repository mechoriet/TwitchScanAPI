using System;

namespace TwitchScanAPI.Models.Twitch.Channel
{
    public class ChannelInformation
    {
        public ChannelInformation(long viewers, string title, string game, DateTime uptime, string thumbnail,
            string streamType, bool isOnline, string id)
        {
            Viewers = viewers;
            Title = title;
            Game = game;
            Uptime = uptime;
            Thumbnail = thumbnail;
            StreamType = streamType;
            IsOnline = isOnline;
            Id = id;
        }

        public ChannelInformation(bool isOnline)
        {
            Viewers = 0;
            Title = string.Empty;
            Game = string.Empty;
            Uptime = DateTime.MinValue;
            Thumbnail = string.Empty;
            StreamType = string.Empty;
            IsOnline = isOnline;
            Id = string.Empty;
        }

        public string Id { get; set; }
        public long Viewers { get; set; }
        public string Title { get; set; }
        public string Game { get; set; }
        public DateTime Uptime { get; set; }
        public string Thumbnail { get; set; }
        public string StreamType { get; set; }
        public bool IsOnline { get; set; }
    }
}