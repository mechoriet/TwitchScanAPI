using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TwitchScanAPI.Models.Twitch.Chat
{
    public record TwitchChatMessage(
        string Username = "",
        string Message = "",
        string? ColorHex = null,
        int Bits = 0,
        double BitsInDollars = 0.0,
        List<TwitchEmote>? Emotes = null,
        bool FirstTime = false)
    {
        public List<TwitchEmote> Emotes { get; init; } = Emotes ?? [];
    }

    public record TwitchEmote(
        string Id,
        string Name,
        string ImageUrl,
        int StartIndex,
        int EndIndex)
    {
        public TwitchEmote(string id, string name, int startIndex, int endIndex)
            : this(id, name, GenerateImageUrl(id), startIndex, endIndex)
        {
        }

        public TwitchEmote(string id, string message)
            : this(id, message, GenerateImageUrl(id), 0, 0)
        {
            var startIndex = message.IndexOf(Name, StringComparison.Ordinal);
            if (startIndex != -1)
            {
                StartIndex = startIndex;
                EndIndex = startIndex + Name.Length;
            }
            else
            {
                // Handle case when the name is not found in the message
                StartIndex = EndIndex = -1; // You could throw an exception or handle it as needed
            }
        }

        public TwitchEmote(string id, string name, string imageUrl, Match match)
            : this(id, name, imageUrl, match.Index, match.Index + match.Length)
        {
        }

        private const string Prefix = "https://static-cdn.jtvnw.net/emoticons/v2/";
        private const string Suffix = "/default/dark/1.0";

        private static string GenerateImageUrl(string id)
        {
            var url = string.Concat(Prefix, id, Suffix);

            // Check if already interned, if not then intern it
            return string.IsInterned(url) ?? string.Intern(url);
        }
    }
}