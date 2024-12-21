namespace TwitchScanAPI.Models.Twitch.Emotes
{
    public class MergedEmote
    {
        public MergedEmote(string id, string name, string url)
        {
            Id = id;
            Name = name;
            Url = url;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
    }
}