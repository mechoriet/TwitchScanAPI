﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Configuration;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchScanAPI.Global;
using TwitchScanAPI.Models.Twitch.Channel;
using TwitchScanAPI.Models.Twitch.Emotes;
using TwitchScanAPI.Services;

namespace TwitchScanAPI.Data.Twitch.Manager
{
    public class TwitchClientManager : IDisposable
    {
        private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(10);
        private readonly string _channelName;
        private readonly IConfiguration _configuration;
        
        private static readonly ClientOptions ClientOptions = new()
        {
            MessagesAllowedInPeriod = 750,
            ThrottlingPeriod = TimeSpan.FromSeconds(30)
        };
        private WebSocketClient? _customClient;

        // BetterTTV & 7TV
        private readonly Timer _reconnectTimer;
        private readonly TimeSpan _retryInterval = TimeSpan.FromSeconds(30);
        private readonly TwitchAPI _api = new();
        private ChannelInformation? _cachedChannelInformation;
        private TwitchClient? _client;
        private bool _fetching;
        private bool _isReconnecting;

        private DateTime _lastFetchTime;
        public List<MergedEmote>? ExternalChannelEmotes;

        // Constructor
        private TwitchClientManager(string channelName, IConfiguration configuration)
        {
            _channelName = channelName;
            _configuration = configuration;
            ConfigureTwitchApi();

            _reconnectTimer = new Timer(_retryInterval.TotalMilliseconds) { AutoReset = false };
            _reconnectTimer.Elapsed += async (_, _) => await HandleReconnectAsync();
        }

        private bool IsOnline { get; set; }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            DisconnectClient();
            _reconnectTimer.Dispose();
            _customClient?.Dispose();
        }

        // Events to expose
        public event EventHandler<OnMessageReceivedArgs>? OnMessageReceived;
        public event EventHandler<OnUserJoinedArgs>? OnUserJoined;
        public event EventHandler<OnUserLeftArgs>? OnUserLeft;
        public event EventHandler<OnNewSubscriberArgs>? OnNewSubscriber;
        public event EventHandler<OnReSubscriberArgs>? OnReSubscriber;
        public event EventHandler<OnGiftedSubscriptionArgs>? OnGiftedSubscription;
        public event EventHandler<OnCommunitySubscriptionArgs>? OnCommunitySubscription;
        public event EventHandler<OnRaidNotificationArgs>? OnRaidNotification;
        public event EventHandler<OnUserBannedArgs>? OnUserBanned;
        public event EventHandler<OnMessageClearedArgs>? OnMessageCleared;
        public event EventHandler<OnUserTimedoutArgs>? OnUserTimedOut;
        public event EventHandler<OnChannelStateChangedArgs>? OnChannelStateChanged;
        public event EventHandler<ChannelInformation>? OnConnectionChanged;
        public event EventHandler? OnDisconnected;

        // Factory method
        public static async Task<TwitchClientManager?> CreateAsync(string channelName, IConfiguration configuration)
        {
            var manager = new TwitchClientManager(channelName, configuration);
            var channelInformation = await manager.GetChannelInfoAsync();
            manager.ExternalChannelEmotes = await EmoteService.GetChannelEmotesAsync(channelInformation.Id);
            await manager.StartClientAsync();
            return manager;
        }

        private void ConfigureTwitchApi()
        {
            _api.Settings.ClientId = _configuration.GetValue<string>(Variables.TwitchClientId);
            _api.Settings.Secret = _configuration.GetValue<string>(Variables.TwitchClientSecret);
        }

        public async Task<ChannelInformation> AttemptConnectionAsync()
        {
            var channelInfo = await GetChannelInfoAsync();
            if (channelInfo.IsOnline)
                await StartClientAsync();
            else
                ScheduleReconnect();

            return channelInfo;
        }

        private Task HandleReconnectAsync()
        {
            _isReconnecting = false;
            return AttemptConnectionAsync();
        }

        private void ScheduleReconnect()
        {
            if (_isReconnecting) return;
            _isReconnecting = true;
            _reconnectTimer.Start();
        }

        private async Task StartClientAsync()
        {
            if (_client?.IsConnected == true) return;

            var credentials = new ConnectionCredentials(
                _configuration.GetValue<string>(Variables.TwitchChatName),
                _configuration.GetValue<string>(Variables.TwitchOauthKey));

            try
            {
                _customClient?.Dispose();
                _customClient = new WebSocketClient(ClientOptions);
                _client = new TwitchClient(_customClient) { AutoReListenOnException = true };
                _client.Initialize(credentials, _channelName);

                SubscribeToClientEvents(_client);

                _client.Connect();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting Twitch client: {ex.Message}");
                await Task.Delay(5000);
                ScheduleReconnect();
            }
        }

        private void SubscribeToClientEvents(TwitchClient client)
        {
            // Subscribe all events
            client.OnMessageReceived += OnMessageReceivedHandler;
            client.OnUserJoined += OnUserJoinedHandler;
            client.OnUserLeft += OnUserLeftHandler;
            client.OnNewSubscriber += OnNewSubscriberHandler;
            client.OnReSubscriber += OnReSubscriberHandler;
            client.OnGiftedSubscription += OnGiftedSubscriptionHandler;
            client.OnCommunitySubscription += OnCommunitySubscriptionHandler;
            client.OnRaidNotification += OnRaidNotificationHandler;
            client.OnUserBanned += OnUserBannedHandler;
            client.OnMessageCleared += OnMessageClearedHandler;
            client.OnUserTimedout += OnUserTimedOutHandler;
            client.OnChannelStateChanged += OnChannelStateChangedHandler;
            client.OnDisconnected += OnTwitchDisconnectedHandler;
            client.OnReconnected += OnTwitchReconnectedHandler;
        }

        private void UnsubscribeFromClientEvents(TwitchClient client)
        {
            // Unsubscribe all events
            client.OnMessageReceived -= OnMessageReceivedHandler;
            client.OnUserJoined -= OnUserJoinedHandler;
            client.OnUserLeft -= OnUserLeftHandler;
            client.OnNewSubscriber -= OnNewSubscriberHandler;
            client.OnReSubscriber -= OnReSubscriberHandler;
            client.OnGiftedSubscription -= OnGiftedSubscriptionHandler;
            client.OnCommunitySubscription -= OnCommunitySubscriptionHandler;
            client.OnRaidNotification -= OnRaidNotificationHandler;
            client.OnUserBanned -= OnUserBannedHandler;
            client.OnMessageCleared -= OnMessageClearedHandler;
            client.OnUserTimedout -= OnUserTimedOutHandler;
            client.OnChannelStateChanged -= OnChannelStateChangedHandler;
            client.OnDisconnected -= OnTwitchDisconnectedHandler;
            client.OnReconnected -= OnTwitchReconnectedHandler;
        }

        // Event handlers
        private void OnMessageReceivedHandler(object? sender, OnMessageReceivedArgs args)
        {
            OnMessageReceived?.Invoke(sender, args);
        }

        private void OnUserJoinedHandler(object? sender, OnUserJoinedArgs args)
        {
            OnUserJoined?.Invoke(sender, args);
        }

        private void OnUserLeftHandler(object? sender, OnUserLeftArgs args)
        {
            OnUserLeft?.Invoke(sender, args);
        }

        private void OnNewSubscriberHandler(object? sender, OnNewSubscriberArgs args)
        {
            OnNewSubscriber?.Invoke(sender, args);
        }

        private void OnReSubscriberHandler(object? sender, OnReSubscriberArgs args)
        {
            OnReSubscriber?.Invoke(sender, args);
        }

        private void OnGiftedSubscriptionHandler(object? sender, OnGiftedSubscriptionArgs args)
        {
            OnGiftedSubscription?.Invoke(sender, args);
        }

        private void OnCommunitySubscriptionHandler(object? sender, OnCommunitySubscriptionArgs args)
        {
            OnCommunitySubscription?.Invoke(sender, args);
        }

        private void OnRaidNotificationHandler(object? sender, OnRaidNotificationArgs args)
        {
            OnRaidNotification?.Invoke(sender, args);
        }

        private void OnUserBannedHandler(object? sender, OnUserBannedArgs args)
        {
            OnUserBanned?.Invoke(sender, args);
        }

        private void OnMessageClearedHandler(object? sender, OnMessageClearedArgs args)
        {
            OnMessageCleared?.Invoke(sender, args);
        }

        private void OnUserTimedOutHandler(object? sender, OnUserTimedoutArgs args)
        {
            OnUserTimedOut?.Invoke(sender, args);
        }

        private void OnChannelStateChangedHandler(object? sender, OnChannelStateChangedArgs args)
        {
            OnChannelStateChanged?.Invoke(sender, args);
        }

        private void OnTwitchDisconnectedHandler(object? sender, EventArgs e)
        {
            Console.WriteLine("Twitch client disconnected. Attempting to reconnect...");
            // You can optionally trigger custom logic here or just rely on TwitchLib's internal reconnect logic
        }

        private void OnTwitchReconnectedHandler(object? sender, EventArgs e)
        {
            Console.WriteLine("Twitch client reconnected successfully.");
        }

        public async Task<ChannelInformation> GetChannelInfoAsync()
        {
            // Use cached info if valid
            if (_fetching || (_cachedChannelInformation != null && DateTime.UtcNow - _lastFetchTime < _cacheDuration))
                return _cachedChannelInformation ?? new ChannelInformation(false);

            _lastFetchTime = DateTime.UtcNow;
            _fetching = true;
            try
            {
                var streams = await _api.Helix.Streams.GetStreamsAsync(userLogins: [_channelName]);
                var isOnline = streams?.Streams.Any() ?? false;

                switch (IsOnline)
                {
                    case true when !isOnline:
                        OnDisconnected?.Invoke(this, EventArgs.Empty);
                        break;
                    case false when isOnline:
                        ExternalChannelEmotes = await EmoteService.GetChannelEmotesAsync(streams!.Streams[0].UserId);
                        Console.WriteLine($"{_channelName} is now online.");
                        break;
                }

                IsOnline = isOnline;
                _cachedChannelInformation = streams?.Streams.Any() == true
                    ? new ChannelInformation(
                        streams.Streams[0].ViewerCount,
                        streams.Streams[0].Title,
                        streams.Streams[0].GameName,
                        streams.Streams[0].StartedAt,
                        streams.Streams[0].ThumbnailUrl,
                        streams.Streams[0].Type,
                        IsOnline,
                        streams.Streams[0].UserId)
                    : new ChannelInformation(false);

                OnConnectionChanged?.Invoke(this, _cachedChannelInformation);

                return _cachedChannelInformation;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP Request Error: {ex.Message}, Inner: {ex.InnerException?.Message}");
                return new ChannelInformation(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return new ChannelInformation(false);
            }
            finally
            {
                _fetching = false;
            }
        }

        public void DisconnectClient()
        {
            if (_client?.IsConnected != true) return;

            UnsubscribeFromClientEvents(_client);
            _client.Disconnect();
            _client = null;
        }
    }
}