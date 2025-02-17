using System;
using System.Text.Json.Serialization;

namespace TwitchScanAPI.Models.Twitch.Base
{
    public class IdEntity
    {
        [JsonIgnore]
        public Guid Id { get; } = Guid.NewGuid();
    }
}