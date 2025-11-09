using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.PubSub.Events;
using TwitchScanAPI.Utilities.Hermes;

namespace TwitchScanAPI.Utilities;

public class TwitchHermesClient
{

    private ClientWebSocket _webSocket;
    private readonly Uri _uri = new Uri("wss://hermes.twitch.tv/v1?clientId=kimne78kx3ncx6brgo4mv6wki5h1ko");
    private CancellationTokenSource _cts;
    private bool _isReconnecting;

    private Dictionary<string, string> _subscriptionToChannel = new();
    private Dictionary<string, string> _ChanneltoSubscription = new();

    public event EventHandler<string> OnMessageReceived;
    public event EventHandler<Exception> OnErrorOccurred;
    public event EventHandler<ViewerUpdateData> OnViewCountReceived;
    public event EventHandler<OnCommercialArgs> OnCommercialReceived;
    public event EventHandler<onSubscriptionActive> OnSubscriptionActiveChanged;
    public string RecoveryUrl { get; private set; }

    public async Task ConnectAsync()
    {
        if (_webSocket != null && (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.Connecting))
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _isReconnecting = false;

        await ConnectInternalAsync();
    }

    private async Task ConnectInternalAsync()
    {
        _webSocket = new ClientWebSocket();

        try
        {
            await _webSocket.ConnectAsync(_uri, _cts.Token);
            _ = Task.Run(() => ReceiveMessagesAsync(_cts.Token), _cts.Token);
        }
        catch (Exception ex)
        {
            OnErrorOccurred?.Invoke(this,ex);
            await HandleReconnectionAsync();
        }
    }

    public async Task DisconnectAsync()
    {
        if (_cts != null)
        {
            _cts.Cancel();
        }

        if (_webSocket != null)
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
            }
            catch
            {
                // Ignore close errors
            }
            finally
            {
                _webSocket.Dispose();
                _webSocket = null;
            }
        }
    }

    public async Task SendMessageAsync(string message)
    {
        if (_webSocket?.State == WebSocketState.Open)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cts.Token);
        }
    }

    private async Task ReceiveMessagesAsync(CancellationToken token)
    {
        var buffer = new byte[1024 * 4];

        try
        {
            while (!token.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    try
                    {
                        var json = JsonDocument.Parse(message);
                        if (json.RootElement.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "welcome")
                        {
                            if (json.RootElement.TryGetProperty("welcome", out var welcomeElement))
                            {
                                var recoveryUrl = welcomeElement.GetProperty("recoveryUrl").GetString();
                                RecoveryUrl = recoveryUrl;
                            }
                        }

                        if (json.RootElement.TryGetProperty("subscribeResponse", out var subscribeResponse))
                        {
                            Console.WriteLine($"subscription response:{subscribeResponse}");
                        }

                        if (json.RootElement.TryGetProperty("notification", out var notification))
                        {
                            _subscriptionToChannel.TryGetValue(notification.GetProperty("subscription").GetProperty("id").GetString(), out var channelId);
                            if (notification.TryGetProperty("type", out var typeElement) &&
                                typeElement.GetString() == "pubsub")
                            {
                                var pubsubString = notification.GetProperty("pubsub").GetString();
                                using var pubsubDoc = JsonDocument.Parse(pubsubString);
                                var pubsubdata = pubsubDoc.RootElement;
                                var pubsubtype = pubsubdata.GetProperty("type").GetString();
                                switch (pubsubtype)
                                {
                                    case "viewcount":
                                        Console.WriteLine($"ViewCount for {channelId} with value {pubsubdata.GetProperty("viewers").GetInt32()} at time: {DateTime.UtcNow}");
                                        var viewupdateobj = new ViewerUpdateData()
                                        {
                                            Viewers = pubsubdata.GetProperty("viewers").GetInt32(),
                                            ChannelId = channelId
                                        };
                                        OnViewCountReceived.Invoke(this,viewupdateobj);
                                        break;
                                    case "commercial":
                                        Console.WriteLine("Commercial:" + pubsubdata);
                                        Console.WriteLine($"Commercial for channel {channelId}: length = {pubsubdata.GetProperty("length").GetInt32()}, scheduled = {pubsubdata.GetProperty("scheduled").GetBoolean()}");
                                        var commercialobj = new OnCommercialArgs()
                                        {
                                            Length = pubsubdata.GetProperty("length").GetInt32(),
                                            ServerTime = "0",
                                            ChannelId = channelId
                                        };
                                        OnCommercialReceived.Invoke(this, commercialobj);
                                        break;
                                }
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // If not valid JSON, just proceed
                    }
                    OnMessageReceived?.Invoke(this,message);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Acknowledged close", CancellationToken.None);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            OnErrorOccurred?.Invoke(this,ex);
            await HandleReconnectionAsync();
        }
    }

    private async Task HandleReconnectionAsync()
    {
        if (_isReconnecting) return;

        _isReconnecting = true;

        try
        {
            await Task.Delay(5000, _cts.Token); // Wait 5 seconds before reconnecting
            if (!_cts.Token.IsCancellationRequested)
            {
                await ConnectInternalAsync();
            }
        }
        catch (TaskCanceledException)
        {
            // Cancellation requested, do nothing
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
        var json = $@"{{
              ""type"": ""subscribe"",
              ""id"": ""{mainid}"",
              ""subscribe"": {{
                ""id"": ""{subid}"",
                ""type"": ""pubsub"",
                ""pubsub"": {{
                  ""topic"": ""video-playback-by-id.{channelId}""
                }}
              }},
              ""timestamp"": ""{DateTime.UtcNow:O}""
            }}";
        Console.WriteLine($"send json for subscription: {json}");
        _subscriptionToChannel.Add(subid,channelId);
        _ChanneltoSubscription.Add(channelId,subid);
        await SendMessageAsync(json);
    }

    public async Task UnsubscribeFromVideoPlayback(string channelId)
    {
        //TODO: make unsub
    }
}
public class ViewerUpdateData
{
public int Viewers { get; set; }
public string? ChannelId { get; set; }
}

public class onSubscriptionActive
{
    public string? ChannelId { get; set; }
}