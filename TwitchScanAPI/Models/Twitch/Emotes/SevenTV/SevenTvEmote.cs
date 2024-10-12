namespace TwitchScanAPI.Models.Twitch.Emotes.SevenTV
{
    public class SevenTvEmote
    {
        public string id { get; set; }
        public string name { get; set; }
        public SevenTvEmoteData data { get; set; }
        public string url => $"https://cdn.7tv.app/emote/{id}/1x.webp";
    }
}