using System.Collections.Generic;
using TwitchScanAPI.Models.Twitch.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Models.Twitch.Statistics
{
    public class ChatHistory : TimedEntity
    {
        public ChatHistory(string username, List<ChannelMessage> messages)
        {
            Username = username;
            Messages = messages;
        }

        public string Username { get; set; }
        public List<ChannelMessage> Messages { get; set; }
    }
}