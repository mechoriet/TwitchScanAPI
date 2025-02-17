using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using TwitchLib.Api;
using TwitchScanAPI.Data.Twitch.Manager;
using TwitchScanAPI.DbContext;
using TwitchScanAPI.Global;
using TwitchScanAPI.Models.DbUser;
using TwitchScanAPI.Models.Twitch.OAuth;

namespace TwitchScanAPI.Controllers
{
    [Route("[controller]/[action]")]
    [ApiController]
    public class TwitchAuthController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        MongoDbContext context,
        TwitchChannelManager twitchStats)
        : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> ExchangeCode(string code, string redirectUri)
        {
            var clientId = configuration[Variables.TwitchClientId];
            var clientSecret = configuration[Variables.TwitchClientSecret];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                return StatusCode(500, "Twitch API credentials not found");

            using var client = httpClientFactory.CreateClient();
            var response = await client.PostAsync("https://id.twitch.tv/oauth2/token", new FormUrlEncodedContent([
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("redirect_uri", redirectUri)
            ]!));

            if (!response.IsSuccessStatusCode) return BadRequest("Error exchanging code");
            var responseData = await response.Content.ReadAsStringAsync();
            var twitchOAuthResponse = JsonSerializer.Deserialize<TwitchOAuthResponse>(responseData);
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
                TimeSpan.FromSeconds(twitchOAuthResponse.expires_in), twitchUser.DisplayName, twitchUser.Email,
                twitchUser.ProfileImageUrl);

            // See if user already exists than update otherwise insert
            var existingUser = await context.TwitchLogins.Find(x => x.DisplayName == twitchUser.DisplayName)
                .FirstOrDefaultAsync();
            if (existingUser != null)
                await context.TwitchLogins.FindOneAndUpdateAsync(
                    Builders<TwitchLogin>.Filter.Eq(x => x.DisplayName, twitchUser.DisplayName),
                    Builders<TwitchLogin>.Update
                        .Set(x => x.AccessToken, twitchOAuthResponse.access_token)
                        .Set(x => x.RefreshToken, twitchOAuthResponse.refresh_token)
                        .Set(x => x.ExpiresIn, TimeSpan.FromSeconds(twitchOAuthResponse.expires_in))
                );
            else
                await context.TwitchLogins.InsertOneAsync(twitchLogin);

            // Init channel
            await twitchStats.Init(twitchLogin.DisplayName);

            return Ok(twitchLogin);
        }

        [HttpGet]
        public async Task<IActionResult> RefreshToken()
        {
            var refreshHeader = Request.Headers["Authorization"];
            if (refreshHeader.Count == 0) return BadRequest("No refresh token provided");
            var refreshToken = refreshHeader[0];
            var clientId = configuration[Variables.TwitchClientId];
            var clientSecret = configuration[Variables.TwitchClientSecret];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) ||
                string.IsNullOrEmpty(refreshToken)) return StatusCode(500);

            using var client = httpClientFactory.CreateClient();
            var response = await client.PostAsync("https://id.twitch.tv/oauth2/token", new FormUrlEncodedContent([
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("grant_type", "refresh_token")
            ]!));

            if (!response.IsSuccessStatusCode) return BadRequest("Error refreshing token");
            var responseData = await response.Content.ReadAsStringAsync();
            var twitchOAuthResponse = JsonSerializer.Deserialize<TwitchOAuthResponse>(responseData);
            if (twitchOAuthResponse == null) return BadRequest("Error deserializing response");

            // Save new TwitchLogin
            var twitchLogin = await context.TwitchLogins.FindOneAndUpdateAsync(
                Builders<TwitchLogin>.Filter.Eq(x => x.RefreshToken, refreshToken),
                Builders<TwitchLogin>.Update
                    .Set(x => x.AccessToken, twitchOAuthResponse.access_token)
                    .Set(x => x.ExpiresIn, TimeSpan.FromSeconds(twitchOAuthResponse.expires_in))
            );
            twitchLogin.AccessToken = twitchOAuthResponse.access_token;

            // Init channel
            await twitchStats.Init(twitchLogin.DisplayName);

            return Ok(twitchLogin);
        }
    }
}