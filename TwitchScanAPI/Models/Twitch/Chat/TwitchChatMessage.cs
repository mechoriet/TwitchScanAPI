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
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string ImageUrl { get; private set; }
        public int StartIndex { get; private set; }
        public int EndIndex { get; private set; }

        public TwitchEmote(string name, string message)
            : this(name, name, GenerateImageUrl(name), message)
        {
        }

        public TwitchEmote(string id, string name, string message)
            : this(id, name, GenerateImageUrl(id), message)
        {
        }

        public TwitchEmote(string id, string name, string imageUrl, string message)
        {
            Id = id;
            Name = name;
            ImageUrl = imageUrl;
            SetIndices(message);
        }

        private static string GenerateImageUrl(string id)
        {
            return $"https://static-cdn.jtvnw.net/emoticons/v2/{id}/default/dark/1.0";
        }

        private void SetIndices(string message)
        {
            StartIndex = message.IndexOf(Name, StringComparison.Ordinal);
            if (StartIndex != -1)
            {
                EndIndex = StartIndex + Name.Length;
            }
            else
            {
                // Handle case when the name is not found in the message
                StartIndex = EndIndex = -1; // You could throw an exception or handle it as needed
            }
        }
    }
}