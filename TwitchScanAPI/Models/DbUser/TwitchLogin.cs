using System;
using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.DbUser
{
    public class TwitchLogin(
        string accessToken,
        string refreshToken,
        TimeSpan expiresIn,
        string displayName,
        string email,
        string profileImageUrl)
        : TimedEntity
    {
        public string DisplayName { get; set; } = displayName;
        public string Email { get; set; } = email;
        public string ProfileImageUrl { get; set; } = profileImageUrl;
        public string AccessToken { get; set; } = accessToken;
        public string RefreshToken { get; set; } = refreshToken;
        public TimeSpan ExpiresIn { get; set; } = expiresIn;
    }
}