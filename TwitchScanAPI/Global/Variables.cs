using System.Collections.Generic;

namespace TwitchScanAPI.Global
{
    public static class Variables
    {
        public const string TwitchOauthKey = "oauth";
        public const string TwitchChatName = "chatName";
        public static readonly List<string> BotNames = new() { "streamlabs", "nightbot", "fossabot" };
        public const int MaxMessages = 100;
    }
}
