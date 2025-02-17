using System;

namespace TwitchScanAPI.Models.Twitch.Base
{
    public class TimedEntity : IdEntity

    {
        public DateTime Time { get; init; } = DateTime.UtcNow;
    }
}