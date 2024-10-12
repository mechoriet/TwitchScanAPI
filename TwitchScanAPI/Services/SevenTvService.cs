using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Services
{
    public class SevenTvService
    {
        private readonly HttpClient _httpClient;
        private List<SevenTvEmote>? GlobalEmotes { get; set; } = new();

        private SevenTvService()
        {
            _httpClient = new HttpClient();
        }

        public static async Task<SevenTvService> CreateAsync()
        {
            var service = new SevenTvService();
            service.GlobalEmotes = await service.GetGlobalEmotesAsync();
            return service;
        }

        private async Task<List<SevenTvEmote>?> GetGlobalEmotesAsync()
        {
            var response = await _httpClient.GetAsync("https://7tv.io/v3/emote-sets/global");
            var content = await response.Content.ReadAsStringAsync();
            var emoteSet = JsonConvert.DeserializeObject<SevenTvEmoteSet>(content);
            return emoteSet?.emotes;
        }

        public async Task<List<SevenTvEmote>?> GetChannelEmotesAsync(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId)) return null;
            var response = await _httpClient.GetAsync($"https://7tv.io/v3/users/twitch/{channelId}");
            var content = await response.Content.ReadAsStringAsync();
            var channelEmoteSet = JsonConvert.DeserializeObject<SevenTvChannelEmoteSet>(content);
            var emotes = new List<SevenTvEmote>();
            if (channelEmoteSet?.emote_set?.emotes == null) return emotes;
            emotes.AddRange(channelEmoteSet.emote_set.emotes);
            if (GlobalEmotes != null) emotes.AddRange(GlobalEmotes);
            return emotes;
        }
    }

    // Simplified models focusing on necessary properties
    public class SevenTvEmote
    {
        public string id { get; set; }
        public string name { get; set; }
        public SevenTvEmoteData data { get; set; }
        public string url => $"https://cdn.7tv.app/emote/{id}/1x.webp";
    }

    public class SevenTvEmoteData
    {
        public bool animated { get; set; }
    }

    public class SevenTvEmoteSet
    {
        public List<SevenTvEmote> emotes { get; set; }
    }

    public class SevenTvChannelEmoteSet
    {
        public SevenTvEmoteSet emote_set { get; set; }
    }
}
