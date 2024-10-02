// TwitchClientManager.cs

using System;
using System.Collections.Generic;
using System.Linq;
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

namespace TwitchScanAPI.Data.Twitch.Manager
{
    public class TwitchClientManager : IDisposable
    {
        private TwitchClient? _client;
        public readonly TwitchAPI Api = new();
        private readonly string _channelName;
        private readonly IConfiguration _configuration;
        private readonly Timer _reconnectTimer;
        private readonly TimeSpan _retryInterval = TimeSpan.FromSeconds(30);
        private bool _isReconnecting;
        public bool IsConnected => _client?.IsConnected == true;

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
        public event EventHandler<bool>? OnConnected; 

        public TwitchClientManager(string channelName, IConfiguration configuration)
        {
            _channelName = channelName;
            _configuration = configuration;
            ConfigureTwitchApi();

            _reconnectTimer = new Timer(_retryInterval.TotalMilliseconds)
            {
                AutoReset = false
            };
            _reconnectTimer.Elapsed += async (_, _) =>
            {
                _isReconnecting = false;
                await AttemptConnectionAsync();
            };
        }

        private void ConfigureTwitchApi()
        {
            var clientId = _configuration.GetValue<string>(Variables.TwitchClientId);
            var clientSecret = _configuration.GetValue<string>(Variables.TwitchClientSecret);

            Api.Settings.ClientId = clientId;
            Api.Settings.Secret = clientSecret;
        }

        public async Task AttemptConnectionAsync()
        {
            var isOnline = await IsChannelOnlineAsync();
            OnConnected?.Invoke(this, isOnline);
            
            if (isOnline)
            {
                await StartClientAsync();
            }
            else
            {
                ScheduleReconnect();
            }
        }

        private void ScheduleReconnect()
        {
            if (_isReconnecting)
                return;

            _isReconnecting = true;
            _reconnectTimer.Start();
        }

        private Task StartClientAsync()
        {
            if (_client is { IsConnected: true })
                return Task.CompletedTask;

            var credentials = new ConnectionCredentials(
                _configuration.GetValue<string>(Variables.TwitchChatName),
                _configuration.GetValue<string>(Variables.TwitchOauthKey));

            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };

            var customClient = new WebSocketClient(clientOptions);
            _client = new TwitchClient(customClient)
            {
                AutoReListenOnException = true,
            };
            _client.Initialize(credentials, _channelName);

            SubscribeToClientEvents();

            _client.Connect();
            return Task.CompletedTask;
        }

        private void SubscribeToClientEvents()
        {
            if (_client == null) return;

            _client.OnMessageReceived += (sender, args) => OnMessageReceived?.Invoke(sender, args);
            _client.OnUserJoined += (sender, args) => OnUserJoined?.Invoke(sender, args);
            _client.OnUserLeft += (sender, args) => OnUserLeft?.Invoke(sender, args);
            _client.OnNewSubscriber += (sender, args) => OnNewSubscriber?.Invoke(sender, args);
            _client.OnReSubscriber += (sender, args) => OnReSubscriber?.Invoke(sender, args);
            _client.OnGiftedSubscription += (sender, args) => OnGiftedSubscription?.Invoke(sender, args);
            _client.OnCommunitySubscription += (sender, args) => OnCommunitySubscription?.Invoke(sender, args);
            _client.OnRaidNotification += (sender, args) => OnRaidNotification?.Invoke(sender, args);
            _client.OnUserBanned += (sender, args) => OnUserBanned?.Invoke(sender, args);
            _client.OnMessageCleared += (sender, args) => OnMessageCleared?.Invoke(sender, args);
            _client.OnUserTimedout += (sender, args) => OnUserTimedOut?.Invoke(sender, args);
        }

        private async Task<bool> IsChannelOnlineAsync()
        {
            try
            {
                var streams = await Api.Helix.Streams.GetStreamsAsync(userLogins: new List<string> { _channelName });
                return streams?.Streams.Any() ?? false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void DisconnectClient()
        {
            if (_client?.IsConnected != true) return;
            _client.Disconnect();
            _client = null;
        }

        public void Dispose()
        {
            DisconnectClient();
            _reconnectTimer.Stop();
            _reconnectTimer.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
