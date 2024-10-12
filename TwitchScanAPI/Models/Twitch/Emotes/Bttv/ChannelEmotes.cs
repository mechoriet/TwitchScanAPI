namespace TwitchScanAPI.Models.Twitch.Emotes.Bttv
{
    public class ChannelEmotes
    {
        public string id { get; set; }
        public BetterTtvEmote[]? channelEmotes { get; set; }
        public BetterTtvEmote[]? sharedEmotes { get; set; }
    }
}