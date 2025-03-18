using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchLib.PubSub.Events;
using TwitchScanAPI.Global;
using TwitchScanAPI.Models.Twitch.Channel;
using TwitchScanAPI.Models.Twitch.Emotes;
using TwitchScanAPI.Services;

namespace TwitchScanAPI.Data.Twitch.Manager
{
    public class TwitchClientManager : IDisposable
    {
        private static readonly TwitchAPI Api = new();
        private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(10);
        private readonly string _channelName;
        private readonly IConfiguration _configuration;
        private readonly TwitchPubSubManager _pubSubManager;
        private long? ViewerCount { get; set; }
        private long? LastViewerCount { get; set; }
        private bool _isOnline;
        private int _consecutiveStreamStateChecks;

        // Thread-safe property access for IsOnline
        private bool IsOnline
        {
            get => _isOnline;
            set
            {
                if (_isOnline == value) return;
                _isOnline = value;
                _cachedChannelInformation.IsOnline = value;
            }
        }

        private static readonly ClientOptions ClientOptions = new()
        {
            MessagesAllowedInPeriod = 750,
            ThrottlingPeriod = TimeSpan.FromSeconds(30)
        };

        private WebSocketClient? _customClient;

        // BetterTTV & 7TV
        private System.Timers.Timer? _reconnectTimer;
        private System.Timers.Timer? _fetchTimeoutTimer;
        private readonly TimeSpan _retryInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _fetchTimeout = TimeSpan.FromSeconds(15);
        private ChannelInformation _cachedChannelInformation = new(false, null);
        private TwitchClient? _client;
        private bool _fetching;
        private bool _isReconnecting;
        private bool _disposed;
        private readonly Lock _fetchLock = new();
        private CancellationTokenSource? _fetchCancellationTokenSource;

        private DateTime _lastFetchTime;
        public List<MergedEmote>? ExternalChannelEmotes;

        // Constructor
        private TwitchClientManager(string channelName, IConfiguration configuration, TwitchPubSubManager pubSubManager)
        {
            _channelName = channelName;
            _configuration = configuration;
            _pubSubManager = pubSubManager;
            ConfigureTwitchApi();

            _reconnectTimer = new System.Timers.Timer(_retryInterval.TotalMilliseconds) { AutoReset = false };
            _reconnectTimer.Elapsed += async (_, _) => await HandleReconnectAsync();

            _fetchTimeoutTimer = new System.Timers.Timer(_fetchTimeout.TotalMilliseconds) { AutoReset = false };
            _fetchTimeoutTimer.Elapsed += (_, _) => HandleFetchTimeout();
        }

        // Factory method
        public static async Task<TwitchClientManager?> CreateAsync(
            string channelName,
            IConfiguration configuration,
            TwitchPubSubManager pubSubManager)
        {
            var manager = new TwitchClientManager(channelName, configuration, pubSubManager);

            try
            {
                var channelInformation = await Api.Helix.Streams.GetStreamsAsync(
                    userLogins: [channelName]);
                var broadcasterId = channelInformation.Streams.FirstOrDefault()?.Id;
                if (string.IsNullOrEmpty(broadcasterId))
                {
                    try
                    {
                        var user = await Api.Helix.Users.GetUsersAsync(logins: [channelName]);
                        broadcasterId = user?.Users.FirstOrDefault()?.Id;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error fetching user ID for '{channelName}': {ex.Message}");
                    }
                }
                else
                {
                    manager.IsOnline = true;
                }

                if (string.IsNullOrEmpty(broadcasterId))
                {
                    Console.WriteLine($"Could not find broadcaster ID for '{channelName}'");
                    return null;
                }

                // Set the cached channel information
                manager._cachedChannelInformation.Id = broadcasterId;

                // Subscribe to PubSub manager events
                manager.SubscribeToPubSubManagerEvents();

                // Add channel to PubSub
                pubSubManager.SubscribeChannel(broadcasterId, channelName);

                if (!manager.IsOnline) return manager;
                pubSubManager.InvokeStreamUp(broadcasterId);

                return manager;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating client manager for '{channelName}': {ex.Message}");
                manager.Dispose();
                return null;
            }
        }

        private void SubscribeToPubSubManagerEvents()
        {
            _pubSubManager.OnViewCountChanged += (_, args) =>
            {
                if (args.ChannelId == _cachedChannelInformation.Id)
                {
                    ViewerCount = args.Viewers;
                }
            };

            _pubSubManager.OnCommercialStarted += (_, args) =>
            {
                if (args.ChannelId == _cachedChannelInformation.Id)
                {
                    OnCommercial?.Invoke(this, args);
                }
            };

            _pubSubManager.OnStreamUp += (o, args) =>
            {
                if (args.ChannelId != _cachedChannelInformation.Id) return;

                IsOnline = true;
                UpdateChannelEmotes(args.ChannelId);
                Console.WriteLine($"{_channelName} is now online (via PubSub).");
                OnConnectionChanged?.Invoke(this, _cachedChannelInformation);
                _ = StartClientAsync();
            };

            _pubSubManager.OnStreamDown += (_, args) =>
            {
                if (args.ChannelId != _cachedChannelInformation.Id) return;

                var wasOnline = IsOnline;
                IsOnline = false;

                if (!wasOnline) return;
                Console.WriteLine($"{_channelName} is now offline (via PubSub).");
                OnDisconnected?.Invoke(this, EventArgs.Empty);
                OnConnectionChanged?.Invoke(this, _cachedChannelInformation);
            };
        }

        private async void UpdateChannelEmotes(string channelId)
        {
            try
            {
                ExternalChannelEmotes = await EmoteService.GetChannelEmotesAsync(channelId);
                Console.WriteLine($"Loaded {ExternalChannelEmotes?.Count ?? 0} external emotes for {_channelName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading external emotes for {_channelName}: {ex.Message}");
                ExternalChannelEmotes = [];
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            GC.SuppressFinalize(this);
            _disposed = true;

            if (_client != null)
            {
                _client.Disconnect();
                UnsubscribeFromClientEvents(_client);
            }

            try
            {
                _reconnectTimer?.Stop();
                _reconnectTimer?.Dispose();
                _fetchTimeoutTimer?.Stop();
                _fetchTimeoutTimer?.Dispose();
                _customClient?.Dispose();
                _fetchCancellationTokenSource?.Cancel();
                _fetchCancellationTokenSource?.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error during disposal of TwitchClientManager for {_channelName}: {e.Message}");
            }

            _reconnectTimer = null;
            _fetchTimeoutTimer = null;
            _customClient = null;
            _fetchCancellationTokenSource = null;

            // Unsubscribe from PubSub topics if we have a valid ID
            if (!string.IsNullOrEmpty(_cachedChannelInformation.Id))
            {
                _pubSubManager.UnsubscribeChannel(_cachedChannelInformation.Id);
            }
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
        public event EventHandler<OnCommercialArgs>? OnCommercial;
        public event EventHandler? OnDisconnected;

        private void ConfigureTwitchApi()
        {
            Api.Settings.ClientId = _configuration.GetValue<string>(Variables.TwitchClientId);
            Api.Settings.Secret = _configuration.GetValue<string>(Variables.TwitchClientSecret);
        }

        private async Task AttemptConnectionAsync()
        {
            if (_disposed) return;

            switch (IsOnline)
            {
                // If the channel is online, but we're not connected, start the client
                case true when _client is not { IsConnected: true }:
                    await StartClientAsync();
                    break;
                case false when _client?.IsConnected != true:
                    ScheduleReconnect();
                    break;
            }
        }

        public void Reconnect()
        {
            if (_disposed) return;

            _client?.Disconnect();
            var credentials = new ConnectionCredentials(
                _configuration.GetValue<string>(Variables.TwitchChatName),
                _configuration.GetValue<string>(Variables.TwitchOauthKey));
            _client?.SetConnectionCredentials(credentials);
            _client?.Reconnect();
        }

        private void HandleFetchTimeout()
        {
            lock (_fetchLock)
            {
                if (!_fetching) return;

                _fetching = false;
                _fetchCancellationTokenSource?.Cancel();
                _fetchCancellationTokenSource?.Dispose();
                _fetchCancellationTokenSource = null;

                Console.WriteLine($"Fetch operation timed out for channel {_channelName}");
            }
        }

        private async Task HandleReconnectAsync()
        {
            if (_disposed) return;

            _isReconnecting = false;
            await AttemptConnectionAsync();
        }

        private void ScheduleReconnect()
        {
            if (_disposed || _isReconnecting) return;

            _isReconnecting = true;
            _reconnectTimer?.Start();
        }

        private Task StartClientAsync()
        {
            if (_disposed) return Task.CompletedTask;
            if (_client?.IsConnected == true) return Task.CompletedTask;

            var credentials = new ConnectionCredentials(
                _configuration.GetValue<string>(Variables.TwitchChatName),
                _configuration.GetValue<string>(Variables.TwitchOauthKey));
            
            try
            {
                _customClient ??= new WebSocketClient(ClientOptions);

                if (_client != null)
                    UnsubscribeFromClientEvents(_client);

                _client = new TwitchClient(_customClient) { AutoReListenOnException = true };
                _client.Initialize(credentials, _channelName);

                SubscribeToClientEvents(_client);
                _client.Connect();
                Console.WriteLine($"Twitch client connected successfully to {_channelName}");

                // If we successfully connected, update online status
                IsOnline = true;
                OnConnectionChanged?.Invoke(this, _cachedChannelInformation);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting Twitch client for {_channelName}: {ex.Message}");
                ScheduleReconnect();
            }

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
            client.OnConnectionError += OnTwitchConnectionErrorHandler;
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
            client.OnConnectionError -= OnTwitchConnectionErrorHandler;
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
            if (IsOnline)
            {
                // Channel disconnected unexpectedly
                Console.WriteLine($"Twitch client disconnected for {_channelName}. Attempting to reconnect...");
            }

            ScheduleReconnect();
        }

        private void OnTwitchConnectionErrorHandler(object? sender, OnConnectionErrorArgs e)
        {
            Console.WriteLine($"Twitch connection error for {_channelName}: {e.Error.Message}");
            ScheduleReconnect();
        }

        private void OnTwitchReconnectedHandler(object? sender, EventArgs e)
        {
            Console.WriteLine($"Twitch client reconnected successfully to {_channelName}");

            if (_client == null) return;
            UnsubscribeFromClientEvents(_client);
            SubscribeToClientEvents(_client);
        }

        public async Task<ChannelInformation> GetChannelInfoAsync(bool forceRefresh = false)
        {
            if (_disposed) return _cachedChannelInformation;

            // Use cached info if valid and not forcing refresh
            if (!forceRefresh && !_fetching && DateTime.UtcNow - _lastFetchTime < _cacheDuration)
                return _cachedChannelInformation;

            lock (_fetchLock)
            {
                if (_fetching && !forceRefresh) return _cachedChannelInformation;

                _fetching = true;
                _fetchCancellationTokenSource?.Cancel();
                _fetchCancellationTokenSource?.Dispose();
                _fetchCancellationTokenSource = new CancellationTokenSource();
                _fetchTimeoutTimer?.Stop();
                _fetchTimeoutTimer?.Start();
            }

            _lastFetchTime = DateTime.UtcNow;

            try
            {
                var streams = await Api.Helix.Streams.GetStreamsAsync(
                    userLogins: [_channelName]);

                var isOnline = streams?.Streams.Length != 0;

                if (IsOnline != isOnline)
                {
                    _consecutiveStreamStateChecks++;

                    var apiState = isOnline ? "online" : "offline";
                    var pubSubState = IsOnline ? "online" : "offline";

                    Console.WriteLine($"API detected {_channelName} as {apiState}, but PubSub shows {pubSubState}.");

                    if (_consecutiveStreamStateChecks >= 3)
                    {
                        IsOnline = isOnline;
                        _consecutiveStreamStateChecks = 0;
                        Console.WriteLine($"Marked {_channelName} as {apiState} after 3 consecutive checks.");
                        if (!isOnline)
                            OnDisconnected?.Invoke(this, EventArgs.Empty);
                    }
                }
                else
                {
                    _consecutiveStreamStateChecks = 0;
                }

                if (IsOnline && streams?.Streams.Length != 0)
                {
                    var stream = streams!.Streams[0];
                    _cachedChannelInformation = new ChannelInformation(
                        ViewerCount ?? stream.ViewerCount,
                        stream.Title,
                        stream.GameName,
                        stream.StartedAt,
                        stream.ThumbnailUrl,
                        stream.Type,
                        true,
                        stream.UserId);
                }
                else
                {
                    _cachedChannelInformation = new ChannelInformation(IsOnline, _cachedChannelInformation.Id);
                }

                LastViewerCount = ViewerCount;
                OnConnectionChanged?.Invoke(this, _cachedChannelInformation);
                return _cachedChannelInformation;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Channel info fetch cancelled for {_channelName}");
                return _cachedChannelInformation;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine(
                    $"HTTP Request Error for {_channelName}: {ex.Message}, Inner: {ex.InnerException?.Message}");
                return _cachedChannelInformation;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting channel info for {_channelName}: {ex.Message}");
                return _cachedChannelInformation;
            }
            finally
            {
                lock (_fetchLock)
                {
                    _fetching = false;
                    _fetchTimeoutTimer?.Stop();
                }
            }
        }
    }
}