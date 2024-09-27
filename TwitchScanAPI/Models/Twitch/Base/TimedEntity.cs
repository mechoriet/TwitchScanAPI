using System;

namespace TwitchScanAPI.Models.Twitch.Base
{
    public class TimedEntity
    {
        public DateTime Time { get; set; } = DateTime.UtcNow;
    }
}