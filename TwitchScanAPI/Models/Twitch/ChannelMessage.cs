using System;
using TwitchLib.Client.Models;
using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch
{
    public class ChannelMessage : TimedEntity
    {
        public ChannelMessage(string channel, ChatMessage chatMessage)
        {
            Channel = channel;
            ChatMessage = chatMessage;
        }

        public string Channel { get; set; }
        public ChatMessage ChatMessage { get; set; }
    }
}
