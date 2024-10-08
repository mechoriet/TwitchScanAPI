using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using TwitchLib.Api;
using TwitchScanAPI.Controllers.Annotations;
using TwitchScanAPI.Data.Twitch.Manager;
using TwitchScanAPI.DbContext;
using TwitchScanAPI.Global;
using TwitchScanAPI.Models.DbUser;
using TwitchScanAPI.Models.Twitch.OAuth;

namespace TwitchScanAPI.Controllers
{
    [Route("[controller]/[action]")]
    [ApiController]
    public class TwitchAuthController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly MongoDbContext _context;
        private readonly TwitchChannelManager _twitchStats;

        public TwitchAuthController(IHttpClientFactory httpClientFactory, IConfiguration configuration, MongoDbContext context, TwitchChannelManager twitchStats)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _context = context;
            _twitchStats = twitchStats;
        }

        [HttpGet]
        public async Task<IActionResult> ExchangeCode(string code, string redirectUri)
        {
            var clientId = _configuration[Variables.TwitchClientId];
            var clientSecret = _configuration[Variables.TwitchClientSecret];
            
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                return StatusCode(500, "Twitch API credentials not found");
            }

            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsync($"https://id.twitch.tv/oauth2/token", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("redirect_uri", redirectUri)
            }!));

            if (!response.IsSuccessStatusCode) return BadRequest("Error exchanging code");
            var responseData = await response.Content.ReadAsStringAsync();
            var twitchOAuthResponse = System.Text.Json.JsonSerializer.Deserialize<TwitchOAuthResponse>(responseData);
            if (twitchOAuthResponse == null) return BadRequest("Error deserializing response");

            var api = new TwitchAPI
            {
                Settings =
                {
                    ClientId = clientId,
                    AccessToken = twitchOAuthResponse.access_token
                }
            };
            var user = await api.Helix.Users.GetUsersAsync();
            if (user.Users.Length == 0) return BadRequest("Error getting user data");
            var twitchUser = user.Users[0];
            
            // New TwitchLogin
            var twitchLogin = new TwitchLogin(twitchOAuthResponse.access_token, twitchOAuthResponse.refresh_token,
                TimeSpan.FromSeconds(twitchOAuthResponse.expires_in), twitchUser.DisplayName, twitchUser.Email, twitchUser.ProfileImageUrl);
            
            // See if user already exists than update otherwise insert
            var existingUser = await _context.TwitchLogins.Find(x => x.DisplayName == twitchUser.DisplayName).FirstOrDefaultAsync();
            if (existingUser != null)
            {
                await _context.TwitchLogins.FindOneAndUpdateAsync(
                    Builders<TwitchLogin>.Filter.Eq(x => x.DisplayName, twitchUser.DisplayName),
                    Builders<TwitchLogin>.Update
                        .Set(x => x.AccessToken, twitchOAuthResponse.access_token)
                        .Set(x => x.RefreshToken, twitchOAuthResponse.refresh_token)
                        .Set(x => x.ExpiresIn, TimeSpan.FromSeconds(twitchOAuthResponse.expires_in))
                );
            }
            else
            {
                await _context.TwitchLogins.InsertOneAsync(twitchLogin);
            }
            
            // Init channel
            await _twitchStats.Init(twitchLogin.DisplayName);
            
            return Ok(twitchLogin);
        }
        
        [HttpGet]
        public async Task<IActionResult> RefreshToken(string refreshToken)
        {
            var clientId = _configuration[Variables.TwitchClientId];
            var clientSecret = _configuration[Variables.TwitchClientSecret];
            
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                return StatusCode(500, "Twitch API credentials not found");
            }

            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsync($"https://id.twitch.tv/oauth2/token", new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("grant_type", "refresh_token")
            }!));

            if (!response.IsSuccessStatusCode) return BadRequest("Error refreshing token");
            var responseData = await response.Content.ReadAsStringAsync();
            var twitchOAuthResponse = System.Text.Json.JsonSerializer.Deserialize<TwitchOAuthResponse>(responseData);
            if (twitchOAuthResponse == null) return BadRequest("Error deserializing response");
            
            // Save new TwitchLogin
            var twitchLogin = await _context.TwitchLogins.FindOneAndUpdateAsync(
                Builders<TwitchLogin>.Filter.Eq(x => x.RefreshToken, refreshToken),
                Builders<TwitchLogin>.Update
                    .Set(x => x.AccessToken, twitchOAuthResponse.access_token)
                    .Set(x => x.ExpiresIn, TimeSpan.FromSeconds(twitchOAuthResponse.expires_in))
            );
            twitchLogin.AccessToken = twitchOAuthResponse.access_token;
            
            // Init channel
            await _twitchStats.Init(twitchLogin.DisplayName);
            
            return Ok(twitchLogin);
        }
    }

}