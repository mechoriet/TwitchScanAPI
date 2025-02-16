using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.Chat
{
    public class ChannelMessage(string channel, TwitchChatMessage chatMessage) : TimedEntity
    {
        public string Channel { get; set; } = channel;
        public TwitchChatMessage ChatMessage { get; set; } = chatMessage;
    }
}