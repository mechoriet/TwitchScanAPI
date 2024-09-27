using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch
{
    public class ClearedMessage : TimedEntity
    {
        public string Message { get; set; }
        public string TargetMessageId { get; set; }
        public string TmiSentTs { get; set; }
    }
}