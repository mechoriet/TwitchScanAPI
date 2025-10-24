using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TwitchLib.Api;
using TwitchLib.Client.Events;
using TwitchScanAPI.Global;
using TwitchScanAPI.Models.Twitch.Channel;
using TwitchScanAPI.Models.Twitch.Emotes;
using TwitchScanAPI.Services;
using Timer = System.Timers.Timer;

namespace TwitchScanAPI.Data.Twitch.Manager
{
    public class TwitchClientManager : IDisposable
    {
        private static readonly TwitchAPI Api = new();
        private readonly string _channelName;
        private string _channelId;
        private readonly IConfiguration _configuration;
        private readonly SharedTwitchClientManager _sharedTwitchClientManager;
        private readonly StreamInfoBatchService _streamInfoBatchService;
        private long? ViewerCount { get; set; }
        private long? LastViewerCount { get; set; }
        private bool _isOnline;
        
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

        // BetterTTV & 7TV
        private Timer? _fetchTimeoutTimer;
        private Timer? _emoteUpdateTimer;
        private Timer? _followerUpdateTimer;
        private ChannelInformation _cachedChannelInformation = new(false, null);
        private bool _fetching;
        private readonly Lock _fetchLock = new();
        private CancellationTokenSource? _fetchCancellationTokenSource;

        private DateTime _lastFetchTime;
        public List<MergedEmote>? ExternalChannelEmotes;

        // Constructor
        private TwitchClientManager(string channelName, IConfiguration configuration, SharedTwitchClientManager sharedTwitchClientManager, StreamInfoBatchService streamInfoBatchService)
        {
            _channelName = channelName;
            _configuration = configuration;
            _sharedTwitchClientManager = sharedTwitchClientManager;
            _streamInfoBatchService = streamInfoBatchService;
            ConfigureTwitchApi();
        }

        // Factory method
        public static async Task<TwitchClientManager?> CreateAsync(
            string channelName,
            IConfiguration configuration,
            SharedTwitchClientManager sharedTwitchClientManager,
            StreamInfoBatchService streamInfoBatchService)
        {
            var manager = new TwitchClientManager(channelName, configuration, sharedTwitchClientManager, streamInfoBatchService);

            try
            {
                var channelInformation = await Api.Helix.Streams.GetStreamsAsync(
                    userLogins: [channelName]);
                var broadcasterId = channelInformation.Streams.FirstOrDefault()?.UserId;
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
                manager._channelId = broadcasterId;
                if (!manager.IsOnline) return manager;

                // If the channel is online, start the client
                manager.OnStreamUp();
                return manager;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating client manager for '{channelName}': {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                manager.Dispose();
                return null;
            }
        }

        private void OnStreamUp()
        {
            if (string.IsNullOrEmpty(_cachedChannelInformation.Id)) return;

            IsOnline = true;
            UpdateChannelEmotes(_cachedChannelInformation.Id);
            StartEmoteUpdateTimer();
            OnConnectionChanged?.Invoke(this, _cachedChannelInformation);
            _sharedTwitchClientManager.JoinChannel(
                _channelName, 
                OnMessageReceivedHandler,
                OnUserJoinedHandler,
                OnUserLeftHandler,
                OnNewSubscriberHandler,
                OnReSubscriberHandler,
                OnGiftedSubscriptionHandler,
                OnCommunitySubscriptionHandler,
                OnUserBannedHandler,
                OnMessageClearedHandler,
                OnUserTimedOutHandler,
                OnChannelStateChangedHandler,
                OnRaidNotificationHandler);
            //do this at last
            Task.Run(async () =>
            {
                await Task.Delay(2000);
                GetFollowersFromStream(true);
                StartFollowerUpdateTimer();
            });
        }

        private void OnStreamDown()
        {
            if (string.IsNullOrEmpty(_cachedChannelInformation.Id)) return;

            var wasOnline = IsOnline;
            IsOnline = false;

            if (!wasOnline) return;
            Console.WriteLine($"{_channelName} is now offline.");
            
            // Don't dispose the client immediately - just mark as offline
            // The IRC connection should stay alive for potential reconnection
            // Only dispose if the client manager itself is being shut down
            StopEmoteUpdateTimer();
            //fetch follows last time
            //stop follow timer
            GetFollowersFromStream(true);
            StopFollowerUpdateTimer();
            
            ExternalChannelEmotes = [];
            OnDisconnected?.Invoke(this, EventArgs.Empty);
            OnConnectionChanged?.Invoke(this, _cachedChannelInformation);
            _sharedTwitchClientManager.LeaveChannel(_channelName);
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
            try
            {
                _fetchTimeoutTimer?.Stop();
                _fetchTimeoutTimer?.Dispose();
                _fetchCancellationTokenSource?.Cancel();
                _fetchCancellationTokenSource?.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error during disposal of TwitchClientManager for {_channelName}: {e.Message}");
            }
            _fetchTimeoutTimer = null;
            _fetchCancellationTokenSource = null;
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
        public event EventHandler<ChannelFollowers> OnFollowerCountUpdate;
        public event EventHandler? OnDisconnected;

        private void ConfigureTwitchApi()
        {
            Api.Settings.ClientId = _configuration.GetValue<string>(Variables.TwitchClientId);
            Api.Settings.Secret = _configuration.GetValue<string>(Variables.TwitchClientSecret);
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

        
        private readonly TimeSpan _cacheDurationOnline = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _cacheDurationOffline = TimeSpan.FromSeconds(10);
        public async Task<ChannelInformation> GetChannelInfoAsync(bool forceRefresh = false)
        {
            lock (_fetchLock)
            {   
                var isCurrentlyOnline = IsOnline; // snapshot to avoid race condition
                var effectiveCacheDuration = isCurrentlyOnline ? _cacheDurationOnline : _cacheDurationOffline;
                // Moved this check inside the lock
                if (!forceRefresh && !_fetching && DateTime.UtcNow - _lastFetchTime < effectiveCacheDuration)
                {
                    return _cachedChannelInformation; 
                }

                if (_fetching && !forceRefresh)
                {
                    return _cachedChannelInformation;
                }
                _fetching = true;
                _fetchCancellationTokenSource?.Cancel();
                _fetchCancellationTokenSource?.Dispose();
                _fetchCancellationTokenSource = new CancellationTokenSource();
                _fetchTimeoutTimer?.Stop();
                _fetchTimeoutTimer?.Start();
            }

            try
            {
                var streams = await _streamInfoBatchService.RequestRawStreamAsync(_channelId);
                _lastFetchTime = DateTime.UtcNow;
                var isOnline = streams != null;

                if (IsOnline != isOnline)
                {
                    if (!isOnline)
                    {
                        OnStreamDown();
                        Console.WriteLine($"{_channelName} is now offline (via API).");
                    }
                    else
                    {
                        OnStreamUp();
                        Console.WriteLine($"{_channelName} is now online (via API).");
                    }
                }

                if (IsOnline && streams != null)
                {
                    _cachedChannelInformation = new ChannelInformation(
                        ViewerCount != null && ViewerCount != LastViewerCount
                            ? ViewerCount.Value
                            : streams.ViewerCount,
                        streams.Title,
                        streams.GameName,
                        streams.StartedAt,
                        streams.ThumbnailUrl,
                        streams.Type,
                        true,
                        streams.UserId);
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
                Console.WriteLine($"TwitchClientManager: Error getting channel info for {_channelName}: {ex.Message}");
                Console.WriteLine($"StackTrace:\n{ex.StackTrace}");
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
        private void StartEmoteUpdateTimer()
        {
            _emoteUpdateTimer = new Timer(TimeSpan.FromMinutes(15).TotalMilliseconds); // Set interval to 15 minutes
            _emoteUpdateTimer.Elapsed += async (_, _) => await UpdateChannelEmotesWithLogging();
            _emoteUpdateTimer.AutoReset = true;
            _emoteUpdateTimer.Start();
        }

        private Task UpdateChannelEmotesWithLogging()
        {
            try
            {
                UpdateChannelEmotes(_cachedChannelInformation.Id);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during emote update: {ex.Message}");
                return Task.CompletedTask;
            }
        }

        private void StopEmoteUpdateTimer()
        {
            _emoteUpdateTimer?.Stop();
            _emoteUpdateTimer?.Dispose();
            _emoteUpdateTimer = null;
        }


        private void StartFollowerUpdateTimer()
        {
            _followerUpdateTimer = new Timer(TimeSpan.FromMinutes(15).TotalMilliseconds); // Set interval to 5 minutes
            _followerUpdateTimer.Elapsed += (_, _) => GetFollowersFromStream(false);
            _followerUpdateTimer.AutoReset = true;
            _followerUpdateTimer.Start();
        }

        private void StopFollowerUpdateTimer()
        {
            _followerUpdateTimer?.Stop();
            _followerUpdateTimer?.Dispose();
            _followerUpdateTimer = null;
        }
        private void GetFollowersFromStream(bool force)
        {
            var result = Api.Helix.Channels.GetChannelFollowersAsync(broadcasterId: _channelId, accessToken: _configuration[Variables.TwitchOauthKey]);
            var resultTotal = result.Result.Total;
            OnFollowerCountUpdate.Invoke(this,new ChannelFollowers(_channelName, resultTotal, force));
            //Console.WriteLine($"{resultTotal} channels followers from {_channelName}");
            
        }
    }
}
