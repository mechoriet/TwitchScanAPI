using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
using TwitchScanAPI.Models.Twitch.Chat;
using TwitchScanAPI.Services;

namespace TwitchScanAPI.Data.Twitch.Manager
{
    public class TwitchClientManager : IDisposable
    {
        private TwitchClient? _client;
        private readonly TwitchAPI _api = new();
        private readonly string _channelName;
        private readonly IConfiguration _configuration;
        private readonly Timer _reconnectTimer;
        private readonly TimeSpan _retryInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(10);
        private bool _isReconnecting;
        private bool IsOnline { get; set; }

        private DateTime _lastFetchTime;
        private ChannelInformation? _cachedChannelInformation;
        
        // BetterTTV
        public List<BetterTtvEmote>? BttvChannelEmotes;
        // 7TV
        public List<SevenTvEmote>? SevenTvChannelEmotes;

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

        // Constructor
        private TwitchClientManager(string channelName, IConfiguration configuration)
        {
            _channelName = channelName;
            _configuration = configuration;
            ConfigureTwitchApi();

            _reconnectTimer = new Timer(_retryInterval.TotalMilliseconds) { AutoReset = false };
            _reconnectTimer.Elapsed += async (_, _) => await HandleReconnectAsync();
        }

        // Factory method
        public static async Task<TwitchClientManager?> CreateAsync(string channelName, IConfiguration configuration, BetterTtvService betterTtvService, SevenTvService sevenTvService)
        {
            var manager = new TwitchClientManager(channelName, configuration);
            var channelInformation = await manager.GetChannelInfoAsync();
            manager.BttvChannelEmotes = await betterTtvService.GetChannelEmotesAsync(channelInformation.Id);
            manager.SevenTvChannelEmotes = await sevenTvService.GetChannelEmotesAsync(channelInformation.Id);
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
            {
                await StartClientAsync();
            }
            else
            {
                ScheduleReconnect();
            }
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

        private Task StartClientAsync()
        {
            if (_client?.IsConnected == true) return Task.CompletedTask;

            var credentials = new ConnectionCredentials(
                _configuration.GetValue<string>(Variables.TwitchChatName),
                _configuration.GetValue<string>(Variables.TwitchOauthKey));

            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };

            var customClient = new WebSocketClient(clientOptions);
            _client = new TwitchClient(customClient) { AutoReListenOnException = true };
            _client.Initialize(credentials, _channelName);

            SubscribeToClientEvents(_client);

            _client.Connect();
            return Task.CompletedTask;
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
        private void OnMessageReceivedHandler(object? sender, OnMessageReceivedArgs args) =>
            OnMessageReceived?.Invoke(sender, args);

        private void OnUserJoinedHandler(object? sender, OnUserJoinedArgs args) => OnUserJoined?.Invoke(sender, args);
        private void OnUserLeftHandler(object? sender, OnUserLeftArgs args) => OnUserLeft?.Invoke(sender, args);

        private void OnNewSubscriberHandler(object? sender, OnNewSubscriberArgs args) =>
            OnNewSubscriber?.Invoke(sender, args);

        private void OnReSubscriberHandler(object? sender, OnReSubscriberArgs args) =>
            OnReSubscriber?.Invoke(sender, args);

        private void OnGiftedSubscriptionHandler(object? sender, OnGiftedSubscriptionArgs args) =>
            OnGiftedSubscription?.Invoke(sender, args);

        private void OnCommunitySubscriptionHandler(object? sender, OnCommunitySubscriptionArgs args) =>
            OnCommunitySubscription?.Invoke(sender, args);

        private void OnRaidNotificationHandler(object? sender, OnRaidNotificationArgs args) =>
            OnRaidNotification?.Invoke(sender, args);

        private void OnUserBannedHandler(object? sender, OnUserBannedArgs args) => OnUserBanned?.Invoke(sender, args);

        private void OnMessageClearedHandler(object? sender, OnMessageClearedArgs args) =>
            OnMessageCleared?.Invoke(sender, args);

        private void OnUserTimedOutHandler(object? sender, OnUserTimedoutArgs args) =>
            OnUserTimedOut?.Invoke(sender, args);

        private void OnChannelStateChangedHandler(object? sender, OnChannelStateChangedArgs args) =>
            OnChannelStateChanged?.Invoke(sender, args);

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
            if (_cachedChannelInformation != null && (DateTime.UtcNow - _lastFetchTime) < _cacheDuration)
            {
                return _cachedChannelInformation;
            }

            try
            {
                var streams = await _api.Helix.Streams.GetStreamsAsync(userLogins: new List<string> { _channelName });
                var isOnline = streams?.Streams.Any() ?? false;

                if (IsOnline && !isOnline)
                {
                    OnDisconnected?.Invoke(this, EventArgs.Empty);
                }

                IsOnline = isOnline;
                _cachedChannelInformation = isOnline && streams?.Streams.Any() == true
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
                _lastFetchTime = DateTime.UtcNow;

                return _cachedChannelInformation;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return new ChannelInformation(false);
            }
        }

        public void DisconnectClient()
        {
            if (_client?.IsConnected != true) return;

            UnsubscribeFromClientEvents(_client);
            _client.Disconnect();
            _client = null;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            DisconnectClient();
            _reconnectTimer.Dispose();
        }
    }
}