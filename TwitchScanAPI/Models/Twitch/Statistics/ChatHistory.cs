using System.Collections.Generic;
using TwitchScanAPI.Models.Twitch.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Models.Twitch.Statistics
{
    public class ChatHistory(string username, List<ChannelMessage> messages) : TimedEntity
    {
        public string Username { get; set; } = username;
        public List<ChannelMessage> Messages { get; set; } = messages;
    }
}