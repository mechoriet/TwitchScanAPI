using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Chat.Emotes;
using TwitchLib.Api.Helix.Models.Videos.GetVideos;
using TwitchScanAPI.Global;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Services
{
    public class TwitchVodService
    {
        private readonly HttpClient _httpClient;
        private readonly TwitchAPI _api = new();
        private static readonly ConcurrentDictionary<string, string> EmoteCache = new();

        public TwitchVodService(IConfiguration configuration)
        {
            var httpSettings = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                UseCookies = true
            };
            _httpClient = new HttpClient(httpSettings);
            // https://github.com/ihabunek/twitch-dl/blob/bcb55be7adf88e9724b28aec53e373e795138f54/twitchdl/__init__.py#L2 Twitch-dl uses this client ID
            _httpClient.DefaultRequestHeaders.Add("Client-ID", "kd1unb4b3q4t58fwlpcbzcbnm76a8fp");
            _api.Helix.Settings.ClientId = configuration[Variables.TwitchClientId];
            _api.Helix.Settings.AccessToken = configuration[Variables.TwitchOauthKey];
        }

        public async Task<List<ChannelMessage>> GetChatMessagesFromVodAsync(string vodUrlOrId, string channelName,
            int startOffsetSeconds = 0)
        {
            var vodId = ExtractVodId(vodUrlOrId);
            var chatMessages = new List<ChannelMessage>();
            string? cursor = null;
            int? contentOffsetSeconds = null;
            bool hasNextPage;

            do
            {
                var response = await FetchChatPageAsync(vodId, cursor, contentOffsetSeconds ?? startOffsetSeconds);
                var comments = response?.SelectToken("[0].data.video.comments.edges");

                if (comments != null)
                {
                    try
                    {
                        chatMessages.AddRange(from comment in comments
                            select comment["node"]
                            into node
                            let createdAt = node?["createdAt"]?.ToObject<DateTime>()
                            let commenterNode = node?["commenter"] as JObject
                            let username = commenterNode?["displayName"]?.ToString() ?? "Unknown"
                            let messageFragments = node?["message"]?["fragments"]
                            let message = messageFragments != null
                                ? string.Join("", messageFragments.Select(f => f["text"]?.ToString()))
                                : string.Empty
                            let emotes = messageFragments?.Where(f => f["emote"] is JObject)
                                .Where(f => !string.IsNullOrEmpty(f["emote"]?["emoteID"]?.ToString()))
                                .Select(f => f["text"]?.ToString())
                                .ToArray()
                            select new ChannelMessage(channelName,
                                    new TwitchChatMessage() { Username = username, Message = message, Emotes = emotes })
                                { Time = createdAt ?? DateTime.MinValue });
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error parsing chat message: " + e.Message + "\n" + e.StackTrace);
                    }
                }

                contentOffsetSeconds = comments?.LastOrDefault()?["node"]?["contentOffsetSeconds"]?.ToObject<int>();
                cursor = comments?.LastOrDefault()?["cursor"]?.ToString() ?? string.Empty;
                var pageInfo = response?.SelectToken("[0].data.video.comments.pageInfo");
                hasNextPage = pageInfo?["hasNextPage"]?.ToObject<bool>() ?? false;
            } while (contentOffsetSeconds != null && hasNextPage);

            return chatMessages;
        }

        private async Task<JArray?> FetchChatPageAsync(string vodId, string? cursor, int contentOffsetSeconds)
        {
            var requestBody = $@"
            [
                {{
                    ""operationName"": ""VideoCommentsByOffsetOrCursor"",
                    ""variables"": {{
                        ""videoID"": ""{vodId}"",
                        {(string.IsNullOrEmpty(cursor) ? $@"""contentOffsetSeconds"": {contentOffsetSeconds}" : $@"""cursor"": ""{cursor}""")}
                    }},
                    ""extensions"": {{
                        ""persistedQuery"": {{
                            ""version"": 1,
                            ""sha256Hash"": ""b70a3591ff0f4e0313d126c6a1502d79a1c02baebb288227c582044aa76adf6a""
                        }}
                    }}
                }}
            ]";

            var content = new StringContent(requestBody.Trim(), System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://gql.twitch.tv/gql", content);
            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JArray.Parse(jsonResponse);
        }

        public async Task<GetVideosResponse?> GetVodsFromChannelAsync(string userName)
        {
            // Set up the request to fetch VODs of type 'archive' (which refers to VODs)
            var userId = await GetUserIdFromUserName(userName);
            var videoResponse = await _api.Helix.Videos.GetVideosAsync(userId: userId, first: 100);

            return videoResponse;
        }

        private async Task<string> GetUserIdFromUserName(string userName)
        {
            var userResponse = await _api.Helix.Users.GetUsersAsync(logins: new List<string> { userName });
            return userResponse.Users.FirstOrDefault()?.Id ?? string.Empty;
        }

        private static string ExtractVodId(string urlOrId)
        {
            if (!Uri.TryCreate(urlOrId, UriKind.Absolute, out var uri)) return urlOrId;
            var segments = uri.Segments;
            return segments.Length > 2 && segments[1].TrimEnd('/') == "videos" ? segments[2] : urlOrId;
        }
    }
}