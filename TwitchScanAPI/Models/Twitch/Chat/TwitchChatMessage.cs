using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TwitchScanAPI.Models.Twitch.Chat
{
    public class TwitchChatMessage
    {
        public string Username { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ColorHex { get; set; }
        public int Bits { get; set; }
        public double BitsInDollars { get; set; }
        public List<TwitchEmote> Emotes { get; set; } = [];
        public bool FirstTime { get; set; }
    }

    public class TwitchEmote
    {

        public TwitchEmote(string id, string name, int startIndex, int endIndex)
            : this(id, name, GenerateImageUrl(id), startIndex, endIndex)
        {
        }

        private TwitchEmote(string id, string name, string imageUrl, int startIndex, int endIndex)
        {
            Id = id;
            Name = name;
            ImageUrl = imageUrl;
            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        public TwitchEmote(string id, string message)
        {
            Id = id;
            Name = message;
            ImageUrl = GenerateImageUrl(id);
            SetIndices(message);
        }

        public TwitchEmote(string id, string name, string imageUrl, Match match)
        {
            Id = id;
            Name = name;
            ImageUrl = imageUrl;
            SetIndicesFromMatch(match);
        }

        public string Id { get; private set; }
        public string Name { get; }
        public string ImageUrl { get; private set; }
        public int StartIndex { get; private set; }
        public int EndIndex { get; private set; }

        private void SetIndicesFromMatch(Match match)
        {
            StartIndex = match.Index;
            EndIndex = StartIndex + match.Length;
        }

        private const string Prefix = "https://static-cdn.jtvnw.net/emoticons/v2/";
        private const string Suffix = "/default/dark/1.0";

        private static string GenerateImageUrl(string id)
        {
            var url = string.Concat(Prefix, id, Suffix);
    
            // Check if already interned, if not then intern it
            return string.IsInterned(url) ?? string.Intern(url);
        }

        private void SetIndices(string message)
        {
            StartIndex = message.IndexOf(Name, StringComparison.Ordinal);
            if (StartIndex != -1)
                EndIndex = StartIndex + Name.Length;
            else
                // Handle case when the name is not found in the message
                StartIndex = EndIndex = -1; // You could throw an exception or handle it as needed
        }
    }
}