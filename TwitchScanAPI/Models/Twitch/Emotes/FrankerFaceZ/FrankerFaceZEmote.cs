using System.Collections.Generic;
using Newtonsoft.Json;

namespace TwitchScanAPI.Models.Twitch.Emotes.FrankerFaceZ
{
    public class FrankerFaceZEmote
    {
        [JsonProperty("id")] public int Id { get; set; }

        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("urls")] public Dictionary<string, string> Urls { get; set; }

        [JsonProperty("height")] public int Height { get; set; }

        [JsonProperty("width")] public int Width { get; set; }
    }
}