using Newtonsoft.Json;

namespace TwitchScanAPI.Models.Twitch.Emotes.FrankerFaceZ
{
    public class FrankerFaceZEmoteOwner
    {
        [JsonProperty("_id")] public int Id { get; set; }

        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("display_name")] public string DisplayName { get; set; }
    }
}