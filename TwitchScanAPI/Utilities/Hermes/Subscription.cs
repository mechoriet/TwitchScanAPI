using System;
using System.Text.Json.Serialization;

namespace TwitchScanAPI.Utilities.Hermes;

public record Subscription
{
    [JsonPropertyName("type")]
    public string Type { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; }

    [JsonPropertyName("subscribe")]
    public SubscribeData Subscribe { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }
}

public record SubscribeData
{
    [JsonPropertyName("id")]
    public string Id { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; }

    [JsonPropertyName("pubsub")]
    public PubSub PubSub { get; init; }
}

public record PubSub
{
    [JsonPropertyName("topic")]
    public string Topic { get; init; }
}