using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using TwitchScanAPI.Global;

namespace TwitchScanAPI.Services
{
    public class TwitchAuthService(IConfiguration configuration, HttpClient httpClient)
    {
        // Twitch OAuth API endpoint
        private const string TokenUrl = "https://id.twitch.tv/oauth2/token";

        public async Task<string?> GetOAuthTokenAsync()
        {
            // Get values from IConfiguration
            var oauth = configuration.GetValue<string>(Variables.TwitchOauthKey);
            var refreshToken = configuration.GetValue<string>(Variables.TwitchRefreshToken);
            var clientId = configuration.GetValue<string>(Variables.TwitchClientId);
            var clientSecret = configuration.GetValue<string>(Variables.TwitchClientSecret);

            if (string.IsNullOrEmpty(oauth) || string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(clientId) ||
                string.IsNullOrEmpty(clientSecret)) throw new Exception("Twitch OAuth configuration is missing.");

            var request = new HttpRequestMessage(HttpMethod.Get, "https://id.twitch.tv/oauth2/validate");
            request.Headers.Add("Authorization", $"OAuth {oauth}");
            var authResponse = await httpClient.SendAsync(request);

            var oauthValidResponse = await authResponse.Content.ReadFromJsonAsync<ValidationResponse>();

            // If the token is still valid, return it
            if (oauthValidResponse is { expires_in: >= 3600 }) // Check if the token is valid for at least 1 hour
                return oauth;

            // Request body parameters for token refresh
            var requestBody = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken)
            }!);

            // Send POST request to refresh the token
            var response = await httpClient.PostAsync(TokenUrl, requestBody);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to retrieve OAuth token: {response.ReasonPhrase}");

            // Parse the response and extract the token
            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            return json["access_token"]?.ToString();
        }
    }

    public class ValidationResponse
    {
        public int expires_in { get; set; }
    }
}