using System.Collections.Generic;

namespace TwitchScanAPI.Global
{
    public static class Variables
    {
        public const string TwitchOauthKey = "oauth";
        public const string TwitchRefreshToken = "refreshToken";
        public const string TwitchClientId = "clientId";
        public const string TwitchClientSecret = "clientSecret";

        public static readonly List<string> BotNames =
        [
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
        ];

        public static readonly List<string> Hermesenabledchannels =
        [
            "noraexplorer",
            "extraemily",
            "salmmus",
            "misterarther",
            "itskatchii",
            "xqc",
            "zackrawrr",
            "hasanabi",
            "honeypuu",
            "mechoriet"
        ];
    }
}