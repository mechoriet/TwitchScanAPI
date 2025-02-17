using System;
using System.Text.Json.Serialization;

namespace TwitchScanAPI.Models.Twitch.Base
{
    public class IdEntity
    {
        [JsonIgnore]
        // Setter is used by Entity Framework
        public Guid Id { get; set;  } = Guid.NewGuid();
    }
}