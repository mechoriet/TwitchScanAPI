﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TwitchScanAPI.Models.Twitch.Emotes;
using TwitchScanAPI.Models.Twitch.Emotes.Bttv;
using TwitchScanAPI.Models.Twitch.Emotes.SevenTV;
using TwitchScanAPI.Models.Twitch.Emotes.FrankerFaceZ;

namespace TwitchScanAPI.Services
{
    public class EmoteService
    {
        private readonly HttpClient _httpClient = new();
        private static List<MergedEmote>? _cachedGlobalEmotes;

        public static async Task<EmoteService> CreateAsync()
        {
            var service = new EmoteService();
            try
            {
                _cachedGlobalEmotes ??= await service.GetMergedGlobalEmotesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CreateAsync: {ex.Message}");
            }

            return service;
        }

        // Fetch global emotes with caching
        private async Task<List<SevenTvEmote>?> GetSevenTvGlobalEmotesAsync()
        {
            try
            {
                return (await FetchEmotesAsync<SevenTvEmoteSet>("https://7tv.io/v3/emote-sets/global"))?.emotes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching SevenTV global emotes: {ex.Message}");
                return null;
            }
        }

        private async Task<List<BetterTtvEmote>?> GetBetterTtvGlobalEmotesAsync()
        {
            try
            {
                return await FetchEmotesAsync<List<BetterTtvEmote>>("https://api.betterttv.net/3/cached/emotes/global");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching BetterTTV global emotes: {ex.Message}");
                return null;
            }
        }

        private async Task<List<FrankerFaceZEmote>?> GetFrankerFaceZGlobalEmotesAsync()
        {
            try
            {
                return (await FetchEmotesAsync<FrankerFaceZEmoteSet>("https://api.frankerfacez.com/v1/set/global"))
                    ?.Sets.Values.SelectMany(s => s.Emoticons).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching FrankerFaceZ global emotes: {ex.Message}");
                return null;
            }
        }

        // Public method to get merged emotes for a channel
        public async Task<List<MergedEmote>?> GetChannelEmotesAsync(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId)) return null;

            try
            {
                var sevenTvChannelEmotes = await GetSevenTvChannelEmotesAsync(channelId);
                var bttvChannelEmotes = await GetBetterTtvChannelEmotesAsync(channelId);
                var ffzChannelEmotes = await GetFrankerFaceZChannelEmotesAsync(channelId);

                var allChannelEmotes = new List<MergedEmote>(_cachedGlobalEmotes ?? new List<MergedEmote>());

                if (sevenTvChannelEmotes != null)
                    allChannelEmotes.AddRange(sevenTvChannelEmotes);

                if (bttvChannelEmotes != null)
                    allChannelEmotes.AddRange(bttvChannelEmotes);

                if (ffzChannelEmotes != null)
                    allChannelEmotes.AddRange(ffzChannelEmotes);

                return allChannelEmotes.GroupBy(e => e.Name).Select(g => g.First())
                    .ToList(); // Remove duplicates by name
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching channel emotes for channel {channelId}: {ex.Message}");
                return null;
            }
        }

        // Private method to fetch and merge global emotes
        private async Task<List<MergedEmote>?> GetMergedGlobalEmotesAsync()
        {
            try
            {
                var sevenTvEmotes = await GetSevenTvGlobalEmotesAsync();
                var bttvEmotes = await GetBetterTtvGlobalEmotesAsync();
                var ffzEmotes = await GetFrankerFaceZGlobalEmotesAsync();

                var mergedEmotes = new List<MergedEmote>();

                if (sevenTvEmotes != null)
                {
                    mergedEmotes.AddRange(sevenTvEmotes.Select(e => new MergedEmote(e.id, e.name, e.url)));
                }

                if (bttvEmotes != null)
                {
                    mergedEmotes.AddRange(bttvEmotes.Select(e => new MergedEmote(e.id, e.code, e.url)));
                }

                if (ffzEmotes != null)
                {
                    mergedEmotes.AddRange(ffzEmotes.Select(e =>
                        new MergedEmote(e.Id.ToString(), e.Name, e.Urls.First().Value)));
                }

                return mergedEmotes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error merging global emotes: {ex.Message}");
                return null;
            }
        }

        // Fetch channel-specific SevenTV emotes
        private async Task<List<MergedEmote>?> GetSevenTvChannelEmotesAsync(string channelId)
        {
            try
            {
                var channelEmoteSet =
                    await FetchEmotesAsync<SevenTvChannelEmoteSet>($"https://7tv.io/v3/users/twitch/{channelId}");
                if (channelEmoteSet?.emote_set?.emotes == null) return null;

                return channelEmoteSet.emote_set.emotes
                    .Select(e => new MergedEmote(e.id, e.name, e.url))
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching SevenTV emotes for channel {channelId}: {ex.Message}");
                return null;
            }
        }

        // Fetch channel-specific BetterTTV emotes
        private async Task<List<MergedEmote>?> GetBetterTtvChannelEmotesAsync(string channelId)
        {
            try
            {
                var channelEmotes =
                    await FetchEmotesAsync<ChannelEmotes>(
                        $"https://api.betterttv.net/3/cached/users/twitch/{channelId}");
                if (channelEmotes == null) return null;

                var emotes = new List<MergedEmote>();

                if (channelEmotes.channelEmotes != null)
                {
                    emotes.AddRange(channelEmotes.channelEmotes.Select(e => new MergedEmote(e.id, e.code, e.url)));
                }

                if (channelEmotes.sharedEmotes != null)
                {
                    emotes.AddRange(channelEmotes.sharedEmotes.Select(e => new MergedEmote(e.id, e.code, e.url)));
                }

                return emotes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching BetterTTV emotes for channel {channelId}: {ex.Message}");
                return null;
            }
        }

        // Fetch channel-specific FrankerFaceZ emotes
        private async Task<List<MergedEmote>?> GetFrankerFaceZChannelEmotesAsync(string channelId)
        {
            try
            {
                var channelEmoteSet =
                    await FetchEmotesAsync<FrankerFaceZChannelEmoteSet>(
                        $"https://api.frankerfacez.com/v1/room/id/{channelId}");
                if (channelEmoteSet?.Sets == null) return null;

                var emotes = new List<MergedEmote>();

                foreach (var set in channelEmoteSet.Sets.Values)
                {
                    emotes.AddRange(set.Emoticons.Select(e =>
                        new MergedEmote(e.Id.ToString(), e.Name, e.Urls.First().Value)));
                }

                return emotes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching FrankerFaceZ emotes for channel {channelId}: {ex.Message}");
                return null;
            }
        }

        // Generic method to fetch and deserialize emotes
        private async Task<T?> FetchEmotesAsync<T>(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to fetch emotes from {url}. Status Code: {response.StatusCode}");
                    return default;
                }

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching emotes from {url}: {ex.Message}");
                return default;
            }
        }
    }
}