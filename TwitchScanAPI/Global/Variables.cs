using System.Collections.Generic;

namespace TwitchScanAPI.Global
{
    public static class Variables
    {
        public const string TwitchOauthKey = "oauth";
        public const string TwitchRefreshToken = "refreshToken";
        public const string TwitchClientId = "clientId";
        public const string TwitchClientSecret = "clientSecret";
        public const string TwitchChatName = "chatName";

        public static readonly List<string> BotNames = new()
        {
            "streamlabs",
            "nightbot",
            "fossabot",
            "streamelements",
            "moobot",
            "soundalerts",
            "own3d",
            "creatisbot",
            "tangibot",
            "overlayexpert",
            "streamstickers",
            "regressz",
            "botrixoficial"
        };
    }
}