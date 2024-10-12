namespace TwitchScanAPI.Models.Twitch.Emotes.Bttv
{
    public class BetterTtvEmote
    {
        public string id { get; set; }
        public string code { get; set; }
        public string imageType { get; set; }
        public bool animated { get; set; }
        public string userId { get; set; }
        public bool modifier { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public string url => $"https://cdn.betterttv.net/emote/{id}/1x.{imageType}";
    }
}