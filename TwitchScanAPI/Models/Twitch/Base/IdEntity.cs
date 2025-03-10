using System;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TwitchScanAPI.Models.Twitch.Base
{
    public class IdEntity
    {
        [JsonIgnore]
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        [BsonGuidRepresentation(GuidRepresentation.CSharpLegacy)]
        // Setter is used by Entity Framework
        public Guid Id { get; set;  } = Guid.NewGuid();
    }
}