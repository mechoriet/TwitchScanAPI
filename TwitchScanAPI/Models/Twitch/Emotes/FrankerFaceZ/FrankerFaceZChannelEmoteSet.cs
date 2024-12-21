using System.Collections.Generic;
using Newtonsoft.Json;

namespace TwitchScanAPI.Models.Twitch.Emotes.FrankerFaceZ
{
    public class FrankerFaceZChannelEmoteSet
    {
        [JsonProperty("sets")] public Dictionary<string, Set> Sets { get; set; }

        [JsonProperty("default_sets")] public List<int> DefaultSets { get; set; }
    }
}