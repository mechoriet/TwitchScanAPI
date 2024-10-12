using System;
using System.Collections.Generic;

namespace TwitchScanAPI.Models.Twitch.Chat
{
    public class TwitchChatMessage
    {
        public string Username { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<TwitchEmote> Emotes { get; set; } = new();
    }

    public class TwitchEmote
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }

        public TwitchEmote(string name)
        {
            Name = name;
            Id = name;
            ImageUrl = $"https://static-cdn.jtvnw.net/emoticons/v2/{Id}/default/dark/1.0";
        }
        
        public TwitchEmote(string id, string name)
        {
            Id = id;
            Name = name;
            ImageUrl = $"https://static-cdn.jtvnw.net/emoticons/v2/{Id}/default/dark/1.0";
        }
        
        public TwitchEmote(string id, string name, string imageUrl)
        {
            Id = id;
            Name = name;
            ImageUrl = imageUrl;
        }
    }
}