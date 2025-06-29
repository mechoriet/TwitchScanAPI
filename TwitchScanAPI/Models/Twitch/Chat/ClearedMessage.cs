using System;
using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.Chat
{
    public class ClearedMessage : TimedEntity
    {
        public string Message { get; set; }
        public string TargetMessageId { get; set; }
        public string TmiSentTs { get; set; }
    }
}