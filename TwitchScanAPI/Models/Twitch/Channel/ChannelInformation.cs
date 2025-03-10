using System;

namespace TwitchScanAPI.Models.Twitch.Channel
{
    public class ChannelInformation(
        long viewers,
        string title,
        string game,
        DateTime uptime,
        string thumbnail,
        string streamType,
        bool isOnline,
        string id)
    {
        public ChannelInformation(bool isOnline) : this(0, string.Empty, string.Empty, DateTime.MinValue, string.Empty, string.Empty, isOnline, string.Empty)
        {
        }

        public string Id { get; set; } = id;
        public long Viewers { get; set; } = viewers;
        public string Title { get; set; } = title;
        public string Game { get; set; } = game;
        public DateTime Uptime { get; set; } = uptime;
        public string Thumbnail { get; set; } = thumbnail;
        public string StreamType { get; set; } = streamType;
        public bool IsOnline { get; set; } = isOnline;
    }
}