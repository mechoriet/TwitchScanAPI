using TwitchLib.Client.Models;
using TwitchScanAPI.Models.Twitch.Base;
using TwitchScanAPI.Services;

namespace TwitchScanAPI.Models.Twitch.Chat
{
    public class ChannelMessage : TimedEntity
    {
        public ChannelMessage(string channel, TwitchChatMessage chatMessage)
        {
            Channel = channel;
            ChatMessage = chatMessage;
        }

        public string Channel { get; set; }
        public TwitchChatMessage ChatMessage { get; set; }
    }
}
