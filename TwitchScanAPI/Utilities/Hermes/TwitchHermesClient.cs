using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchLib.PubSub.Events;
using TwitchScanAPI.Data.Twitch.Manager;
using TwitchScanAPI.Utilities.Hermes;

namespace TwitchScanAPI.Utilities.Hermes;

public class TwitchHermesClient : IDisposable
{

    private readonly ILogger _logger = LoggerFactoryHelper.CreateLogger<TwitchHermesService>();
    private ClientWebSocket? _webSocket;
    private readonly Uri _uri = new("wss://hermes.twitch.tv/v1?clientId=kimne78kx3ncx6brgo4mv6wki5h1ko");
    private CancellationTokenSource _cts;
    private bool _isReconnecting;
    private bool _isDead;
    private int _reconnectionAttempts = 0;
    private const int MaxReconnectionAttempts = 5;
    private readonly TimeSpan _initialDelay = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _maxDelay = TimeSpan.FromMinutes(5);

    private readonly Dictionary<string, string> _subscriptionToChannel = new();
    private readonly Dictionary<string, string> _channeltoSubscription = new();

    public event EventHandler<string>? OnMessageReceived;
    public event EventHandler<Exception>? OnErrorOccurred;
    public event EventHandler<ViewerUpdateData>? OnViewCountReceived;
    public event EventHandler<OnCommercialArgs>? OnCommercialReceived;
    public event EventHandler<OnSubscriptionActive>? OnSubscriptionActiveChanged;
    public event EventHandler? OnConnectionDead;
    public string? RecoveryUrl { get; private set; }
    public bool IsDead => _isDead;

    public async Task ConnectAsync()
    {
        _cts = new CancellationTokenSource();
        _isReconnecting = false;
        _isDead = false;
        _reconnectionAttempts = 0;

        _logger.LogInformation("Initiating connection to Twitch Hermes WebSocket.");
        await ConnectInternalAsync();
    }

    private async Task ConnectInternalAsync()
    {
        // Dispose old WebSocket if exists
        if (_webSocket != null)
        {
            _webSocket.Dispose();
            _webSocket = null;
        }

        _webSocket = new ClientWebSocket();

        try
        {
            var uriToUse = RecoveryUrl != null ? new Uri(RecoveryUrl) : _uri;
            _logger.LogInformation("Connecting to WebSocket URI: {Uri}", uriToUse);
            await _webSocket.ConnectAsync(uriToUse, _cts.Token);
            _logger.LogInformation("Successfully connected to Twitch Hermes WebSocket.");
            _ = Task.Run(() => ReceiveMessagesAsync(_cts.Token), _cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Twitch Hermes WebSocket.");
            OnErrorOccurred?.Invoke(this,ex);
            // Dispose WebSocket on failure
            if (_webSocket != null)
            {
                _webSocket.Dispose();
                _webSocket = null;
            }
            await HandleReconnectionAsync();
        }
    }

    public async Task DisconnectAsync()
    {
        _logger.LogInformation("Disconnecting from Twitch Hermes WebSocket.");
        await _cts.CancelAsync();

        if (_webSocket != null)
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.Connecting)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
                    _logger.LogInformation("WebSocket closed successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred while closing WebSocket.");
            }
            finally
            {
                _webSocket.Dispose();
                _webSocket = null;
            }
        }
    }

    private async Task SendMessageAsync(string message)
    {
        if (_webSocket?.State == WebSocketState.Open)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cts.Token);
        }
    }

    private readonly byte[] _buffer = new byte[4 * 1024];

    private async Task ReceiveMessagesAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(_buffer), token).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await HandleWebSocketCloseAsync(result);
                    break;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                    continue;

                var message = Encoding.UTF8.GetString(_buffer, 0, result.Count);
                await HandleTextMessageAsync(message);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException)
        {
            _logger.LogInformation("WebSocket receive loop cancelled during shutdown.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WebSocket receive loop.");
            OnErrorOccurred?.Invoke(this, ex);
            // Dispose WebSocket on error
            if (_webSocket != null)
            {
                _webSocket.Dispose();
                _webSocket = null;
            }
            await HandleReconnectionAsync();
        }
    }

    private async Task HandleWebSocketCloseAsync(WebSocketReceiveResult result)
    {
        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Acknowledged close", CancellationToken.None)
                        .ConfigureAwait(false);

        _logger.LogWarning("WebSocket closed: {CloseStatus} - {CloseStatusDescription}", result.CloseStatus, result.CloseStatusDescription);

        // Dispose WebSocket after closing
        if (_webSocket != null)
        {
            _webSocket.Dispose();
            _webSocket = null;
        }

        // Extract recovery URL from close description if available
        if (!string.IsNullOrEmpty(result.CloseStatusDescription))
        {
            var parts = result.CloseStatusDescription.Split(' ');
            foreach (var part in parts)
            {
                if (part.StartsWith("wss://") || part.StartsWith("ws://"))
                {
                    RecoveryUrl = part;
                    _logger.LogInformation("Extracted recovery URL from close message: {RecoveryUrl}", RecoveryUrl);
                    break;
                }
            }
        }

        if (!_isDead)
        {
            await HandleReconnectionAsync();
        }
    }

    private void MarkAsDead()
    {
        if (_isDead) return;
        _isDead = true;
        _logger.LogError("Connection marked as dead due to repeated failures.");
        _ = DisconnectAsync();
        OnConnectionDead?.Invoke(this, EventArgs.Empty);
    }

    private Task HandleTextMessageAsync(string message)
    {
        OnMessageReceived?.Invoke(this, message);

        if (!TryParseJson(message, out var document))
            return Task.CompletedTask;

        using (document)
        {
            var root = document.RootElement;

            if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "welcome")
            {
                HandleWelcomeMessage(root);
                return Task.CompletedTask;
            }

            if (root.TryGetProperty("subscribeResponse", out var subscribeResponse))
            {
                HandleSubscribeResponse(subscribeResponse);
                return Task.CompletedTask;
            }

            if (root.TryGetProperty("notification", out var notification))
            {
                HandleNotification(notification);
            }
        }

        return Task.CompletedTask;
    }

    private static bool TryParseJson(string text, out JsonDocument? document)
    {
        document = null;
        try
        {
            document = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            // Invalid JSON – ignore and continue
            return false;
        }
    }

    private void HandleWelcomeMessage(JsonElement root)
    {
        if (root.TryGetProperty("welcome", out var welcome) &&
            welcome.TryGetProperty("recoveryUrl", out var recoveryUrlElement))
        {
            RecoveryUrl = recoveryUrlElement.GetString();
            _logger.LogInformation("Received recovery URL from welcome message: {RecoveryUrl}", RecoveryUrl);
        }
    }

    private void HandleSubscribeResponse(JsonElement response)
    {
        try
        {
            if (response.GetProperty("result").GetString() != "ok")
                return;

            var subscriptionId = response.GetProperty("subscription").GetProperty("id").GetString();

            if (_subscriptionToChannel.TryGetValue(subscriptionId, out var channelId))
            {
                OnSubscriptionActiveChanged?.Invoke(this, new OnSubscriptionActive
                {
                    ChannelId = channelId,
                    Active = true
                });
                _logger.LogInformation("Subscription confirmed for channel: {ChannelId}", channelId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing subscribeResponse for subscription ID: {SubscriptionId}", response.GetProperty("subscription").GetProperty("id").GetString());
        }
    }

    private void HandleNotification(JsonElement notification)
    {
        var subscriptionId = notification.GetProperty("subscription").GetProperty("id").GetString();

        if (!_subscriptionToChannel.TryGetValue(subscriptionId, out var channelId))
            return;

        if (!notification.TryGetProperty("type", out var typeElem) || typeElem.GetString() != "pubsub")
            return;

        var pubsubJson = notification.GetProperty("pubsub").GetString();
        if (string.IsNullOrEmpty(pubsubJson))
            return;

        if (!TryParseJson(pubsubJson, out var pubsubDoc))
            return;

        using (pubsubDoc)
        {
            if (pubsubDoc == null) return;
            var data = pubsubDoc.RootElement;
            var pubsubType = data.GetProperty("type").GetString();

            switch (pubsubType)
            {
                case "viewcount":
                    HandleViewCount(data, channelId);
                    break;

                case "commercial":
                    HandleCommercial(data, channelId);
                    break;

                default:
                    _logger.LogWarning("Unhandled pubsub type: {PubsubType} for channel {ChannelId}", pubsubType, channelId);
                    break;
            }
        }
    }

    private void HandleViewCount(JsonElement data, string channelId)
    {
        var viewers = data.GetProperty("viewers").GetInt32();
        _logger.LogInformation("View count update for channel {ChannelId}: {Viewers} viewers", channelId, viewers);

        OnViewCountReceived?.Invoke(this, new ViewerUpdateData
        {
            ChannelId = channelId,
            Viewers = viewers
        });
    }

    private void HandleCommercial(JsonElement data, string channelId)
    {
        var length = data.GetProperty("length").GetInt32();
        var scheduled = data.GetProperty("scheduled").GetBoolean();

        _logger.LogInformation("Commercial event for channel {ChannelId}: {Length}s (scheduled: {Scheduled})", channelId, length, scheduled);

        OnCommercialReceived?.Invoke(this, new OnCommercialArgs
        {
            ChannelId = channelId,
            Length = length,
            ServerTime = "0" // Consider extracting real server time if available
        });
    }

    private async Task HandleReconnectionAsync()
    {
        if (_isReconnecting || _isDead) return;

        _isReconnecting = true;

        try
        {
            if (_reconnectionAttempts >= MaxReconnectionAttempts)
            {
                _logger.LogError("Max reconnection attempts ({MaxAttempts}) reached. Marking connection as dead.", MaxReconnectionAttempts);
                MarkAsDead();
                return;
            }

            _reconnectionAttempts++;

            // Exponential backoff: delay = initialDelay * 2^(attempts-1), capped at maxDelay
            var delay = TimeSpan.FromTicks(Math.Min(_initialDelay.Ticks * (1L << (_reconnectionAttempts - 1)), _maxDelay.Ticks));
            _logger.LogWarning("Attempting reconnection {Attempt}/{MaxAttempts} after {DelaySeconds} seconds", _reconnectionAttempts, MaxReconnectionAttempts, delay.TotalSeconds);

            await Task.Delay(delay, _cts.Token);

            if (!_cts.Token.IsCancellationRequested)
            {
                _logger.LogInformation("Reconnection attempt {Attempt} initiated.", _reconnectionAttempts);
                await ConnectInternalAsync();
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Reconnection cancelled.");
        }
        finally
        {
            _isReconnecting = false;
        }
    }

    public async Task SubscribeToVideoPlayback(string channelId)
    {
        var mainid = IdGenerator.MakeId();
        var subid = IdGenerator.MakeId();
        var json = $$"""
                      {
                        "type": "subscribe",
                        "id": "{{mainid}}",
                        "subscribe": {
                          "id": "{{subid}}",
                          "type": "pubsub",
                          "pubsub": {
                            "topic": "video-playback-by-id.{{channelId}}"
                          }
                        },
                        "timestamp": "{{DateTime.UtcNow:O}}"
                      }
                      """;
        _subscriptionToChannel.Add(subid,channelId);
        _channeltoSubscription.Add(channelId,subid);
        _logger.LogInformation("Subscribing to video playback for channel {ChannelId}", channelId);
        await SendMessageAsync(json);
    }

    public async Task UnsubscribeFromVideoPlayback(string channelId)
    {
        // {"type":"unsubscribe","id":"FAiUlUhViNFariSM9_d0h","unsubscribe":{"id":"sVazH5NDG_O7e2YxwJahq"},"timestamp":"2025-10-31T18:34:59.856Z"}

        _channeltoSubscription.TryGetValue(channelId, out var subId);
        var json = $$"""
                      {
                         "type": "unsubscribe",
                         "id": "{{IdGenerator.MakeId()}},
                         "unsubscribe": {
                           "id":{{subId}},
                         },
                         "timestamp": "{{DateTime.UtcNow:0}}"
                      }
                      """;
        try
        {
            _logger.LogInformation("Unsubscribing from video playback for channel {ChannelId}", channelId);
            await SendMessageAsync(json);
            _subscriptionToChannel.Remove(channelId);
            _channeltoSubscription.Remove(channelId);
        }
        catch(Exception err)
        {
            _logger.LogError(err, "Error unsubscribing from video playback for channel {ChannelId}", channelId);
        }
    }

    public void Dispose()
    {
        _ = DisconnectAsync();
    }
}
public class ViewerUpdateData
{
public int Viewers { get; set; }
public string? ChannelId { get; set; }
}

public class OnSubscriptionActive
{
    public string? ChannelId { get; set; }
    public bool Active { get; set; } 
}