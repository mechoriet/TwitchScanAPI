namespace TwitchScanAPI.Models.Twitch.Emotes
{
    public class MergedEmote(string id, string name, string url)
    {
        public string Id { get; set; } = id;
        public string Name { get; set; } = name;
        public string Url { get; set; } = url;
    }
}