using System.Collections.Generic;
using Newtonsoft.Json;

namespace TwitchScanAPI.Models.Twitch.Emotes.FrankerFaceZ
{
    public class FrankerFaceZEmoteSet
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("_type")]
        public int Type { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("sets")]
        public Dictionary<string, Set> Sets { get; set; }
    }

    public class Set
    {
        public long Id { get; set; }
        public long Type { get; set; }
        public object Icon { get; set; }
        public string Title { get; set; }
        public object Css { get; set; }
        public List<FrankerFaceZEmote> Emoticons { get; set; }
    }
}