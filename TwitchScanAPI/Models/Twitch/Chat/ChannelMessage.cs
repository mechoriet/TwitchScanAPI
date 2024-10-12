using TwitchScanAPI.Models.Twitch.Base;

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
