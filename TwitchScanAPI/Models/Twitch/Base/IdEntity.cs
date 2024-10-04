using System;

namespace TwitchScanAPI.Models.Twitch.Base
{
    public class IdEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
    }
}