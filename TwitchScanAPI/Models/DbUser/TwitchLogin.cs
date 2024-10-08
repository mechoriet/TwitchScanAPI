using System;
using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.DbUser
{
    public class TwitchLogin : TimedEntity
    {
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public string ProfileImageUrl { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public TimeSpan ExpiresIn { get; set; }
        
        public TwitchLogin(string accessToken, string refreshToken, TimeSpan expiresIn, string displayName, string email, string profileImageUrl)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            ExpiresIn = expiresIn;
            DisplayName = displayName;
            Email = email;
            ProfileImageUrl = profileImageUrl;
        }
    }
}