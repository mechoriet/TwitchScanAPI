using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using TwitchLib.Api;
using TwitchScanAPI.Global;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Services
{
    public class BetterTtvService
    {
        private readonly HttpClient _httpClient;
        public List<BetterTtvEmote>? GlobalEmotes { get; private set; } = new();

        private BetterTtvService()
        {
            _httpClient = new HttpClient();
        }

        public static async Task<BetterTtvService> CreateAsync()
        {
            var service = new BetterTtvService();
            service.GlobalEmotes = await service.GetGlobalEmotesAsync();
            return service;
        }

        private async Task<List<BetterTtvEmote>?> GetGlobalEmotesAsync()
        {
            var response = await _httpClient.GetAsync("https://api.betterttv.net/3/cached/emotes/global");
            var content = await response.Content.ReadAsStringAsync();
            var emotes = JsonConvert.DeserializeObject<List<BetterTtvEmote>>(content);
            return emotes;
        }

        public async Task<List<BetterTtvEmote>?> GetChannelEmotesAsync(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId)) return null;
            var response = await _httpClient.GetAsync($"https://api.betterttv.net/3/cached/users/twitch/{channelId}");
            var content = await response.Content.ReadAsStringAsync();
            var channelEmotes = JsonConvert.DeserializeObject<ChannelEmotes>(content);
            var emotes = new List<BetterTtvEmote>();
            if (channelEmotes == null) return emotes;
            emotes.AddRange(channelEmotes.channelEmotes);
            emotes.AddRange(channelEmotes.sharedEmotes);
            return emotes;
        }
    }

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
        
        public TwitchEmote ToTwitchEmote()
        {
            return new TwitchEmote(id, code, url);
        }
    }
    
    public class ChannelEmotes
    {
        public string id { get; set; }
        public object[] bots { get; set; }
        public string avatar { get; set; }
        public BetterTtvEmote[] channelEmotes { get; set; }
        public BetterTtvEmote[] sharedEmotes { get; set; }
    }
}