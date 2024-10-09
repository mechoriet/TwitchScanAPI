using System;

namespace TwitchScanAPI.Models.Twitch.Chat
{
    public class TwitchChatMessage
    {
        public string Username { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string?[]? Emotes { get; set; } = Array.Empty<string>();
    }
}