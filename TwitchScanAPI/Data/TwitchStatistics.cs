// TwitchStatistics.cs
#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchScanAPI.Global;
using TwitchScanAPI.Hubs;
using TwitchScanAPI.Models;
using TwitchScanAPI.Models.Enums;

namespace TwitchScanAPI.Data
{
    public class TwitchStatistics : IDisposable
    {
        // Configuration
        private readonly TwitchClient _client;
        private readonly IHubContext<TwitchHub, ITwitchHub> _hubContext;
        private readonly IConfiguration _configuration;
        
        // Channel Information
        public string ChannelName { get; }
        public ConcurrentDictionary<string, string> Users { get; } = new(StringComparer.OrdinalIgnoreCase);
        public ConcurrentBag<ChannelMessage> Messages { get; } = new();
        public ConcurrentBag<ChannelMessage> ObservedMessages { get; } = new();
        public ConcurrentBag<ChannelMessage> ElevatedMessages { get; } = new();
        public ConcurrentBag<object> ClearedMessages { get; } = new();
        public ConcurrentBag<object> TimedOutUsers { get; } = new();
        public ConcurrentBag<object> BannedUsers { get; } = new();
        public ConcurrentBag<object> Subscriptions { get; } = new();
        
        // Words to observe
        private readonly HashSet<string> _wordsToObserve = new(StringComparer.OrdinalIgnoreCase);
        private Regex? _observePatternRegex;

        public TwitchStatistics(string channelName, IHubContext<TwitchHub, ITwitchHub> hubContext, IConfiguration configuration)
        {
            ChannelName = channelName;
            _hubContext = hubContext;
            _configuration = configuration;
            _client = InitializeClient();
            ConnectClient();
        }

        public void AddTextToObserve(string text)
        {
            if (_wordsToObserve.Add(text))
            {
                UpdateRegex();
            }
        }

        private TwitchClient InitializeClient()
        {
            // Fetch the OAuth token from the configuration
            var oauth = _configuration.GetValue<string>(Variables.TwitchOauthKey);
            var twitchChatName = _configuration.GetValue<string>(Variables.TwitchChatName);
            var credentials = new ConnectionCredentials(twitchChatName,oauth);
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            var customClient = new WebSocketClient(clientOptions);
            var client = new TwitchClient(customClient);
            client.Initialize(credentials, ChannelName);

            // Subscribe to events
            client.OnMessageReceived += Client_OnMessageReceived;
            client.OnNewSubscriber += Client_OnNewSubscriber;
            client.OnReSubscriber += Client_OnReSubscriber;
            client.OnGiftedSubscription += Client_OnGiftedSubscription;
            client.OnCommunitySubscription += Client_OnCommunitySubscription;
            client.OnUserTimedout += Client_OnUserTimedOut;
            client.OnMessageCleared += Client_OnMessageCleared;
            client.OnUserBanned += Client_OnUserBanned;
            client.OnUserJoined += Client_OnUserJoined;
            client.OnUserLeft += Client_OnUserLeft;

            return client;
        }

        private void ConnectClient()
        {
            _client.Connect();
        }

        private void UpdateRegex()
        {
            if (_wordsToObserve.Any())
            {
                var pattern = string.Join("|", _wordsToObserve.Select(Regex.Escape));
                _observePatternRegex = new Regex($@"\b({pattern})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            else
            {
                _observePatternRegex = null;
            }
        }

        private void Client_OnUserLeft(object? sender, OnUserLeftArgs e)
        {
            Users.TryRemove(e.Username, out _);
            _hubContext.Clients.Group(ChannelName).ReceiveUserLeft(e.Username);
        }

        private void Client_OnUserJoined(object? sender, OnUserJoinedArgs e)
        {
            Users.TryAdd(e.Username, e.Channel);
            _hubContext.Clients.Group(ChannelName).ReceiveUserJoined(e.Username, e.Channel);
        }

        private async void Client_OnNewSubscriber(object? sender, OnNewSubscriberArgs e)
        {
            var subscription = new
            {
                e.Subscriber,
                Type = SubscriptionType.New,
                Time = DateTime.UtcNow
            };
            Subscriptions.Add(subscription);
            await _hubContext.Clients.Group(ChannelName).ReceiveSubscription(subscription);
        }

        private async void Client_OnCommunitySubscription(object? sender, OnCommunitySubscriptionArgs e)
        {
            var subscription = new
            {
                e.GiftedSubscription,
                Type = SubscriptionType.Community,
                Time = DateTime.UtcNow
            };
            Subscriptions.Add(subscription);
            await _hubContext.Clients.Group(ChannelName).ReceiveSubscription(subscription);
        }

        private async void Client_OnGiftedSubscription(object? sender, OnGiftedSubscriptionArgs e)
        {
            var subscription = new
            {
                e.GiftedSubscription,
                Type = SubscriptionType.Gifted,
                Time = DateTime.UtcNow
            };
            Subscriptions.Add(subscription);
            await _hubContext.Clients.Group(ChannelName).ReceiveSubscription(subscription);
        }

        private async void Client_OnReSubscriber(object? sender, OnReSubscriberArgs e)
        {
            var subscription = new
            {
                e.ReSubscriber,
                Type = SubscriptionType.Re,
                Time = DateTime.UtcNow
            };
            Subscriptions.Add(subscription);
            await _hubContext.Clients.Group(ChannelName).ReceiveSubscription(subscription);
        }

        private async void Client_OnUserBanned(object? sender, OnUserBannedArgs e)
        {
            var bannedUser = new
            {
                e.UserBan.Username,
                e.UserBan.BanReason,
                Time = DateTime.UtcNow
            };
            BannedUsers.Add(bannedUser);
            await _hubContext.Clients.Group(ChannelName).ReceiveBannedUser(bannedUser);
        }

        private async void Client_OnMessageCleared(object? sender, OnMessageClearedArgs e)
        {
            var clearedMessage = new
            {
                e.Message,
                e.TargetMessageId,
                e.TmiSentTs,
                Time = DateTime.UtcNow
            };
            ClearedMessages.Add(clearedMessage);
            await _hubContext.Clients.Group(ChannelName).ReceiveClearedMessage(clearedMessage);
        }

        private async void Client_OnUserTimedOut(object? sender, OnUserTimedoutArgs e)
        {
            var timedOutUser = new
            {
                e.UserTimeout.Username,
                e.UserTimeout.TimeoutReason,
                e.UserTimeout.TimeoutDuration,
                Time = DateTime.UtcNow
            };
            TimedOutUsers.Add(timedOutUser);
            await _hubContext.Clients.Group(ChannelName).ReceiveTimedOutUser(timedOutUser);
        }

        private async void Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            var sendMessage = new ChannelMessage(ChannelName, e.ChatMessage);
            
            Messages.Add(sendMessage);
            if (Messages.Count > Variables.MaxMessages)
            {
                Messages.TryTake(out _);
            }
            
            if (_observePatternRegex == null) return;

            if (!_observePatternRegex.IsMatch(e.ChatMessage.Message)) return;
            
            var channelMessage = new ChannelMessage(ChannelName, e.ChatMessage);
            ObservedMessages.Add(channelMessage);
            await _hubContext.Clients.Group(ChannelName).ReceiveChannelMessage(channelMessage);

            if ((!e.ChatMessage.IsModerator && !e.ChatMessage.IsPartner && !e.ChatMessage.IsStaff &&
                 !e.ChatMessage.IsVip) ||
                Variables.BotNames.Contains(e.ChatMessage.DisplayName, StringComparer.OrdinalIgnoreCase)) return;
            
            ElevatedMessages.Add(channelMessage);
            await _hubContext.Clients.Group(ChannelName).ReceiveElevatedMessage(channelMessage);
        }

        public void Dispose()
        {
            if (_client.IsConnected)
            {
                _client.Disconnect();
            }

            // Unsubscribe from events to prevent memory leaks
            _client.OnMessageReceived -= Client_OnMessageReceived;
            _client.OnNewSubscriber -= Client_OnNewSubscriber;
            _client.OnReSubscriber -= Client_OnReSubscriber;
            _client.OnGiftedSubscription -= Client_OnGiftedSubscription;
            _client.OnCommunitySubscription -= Client_OnCommunitySubscription;
            _client.OnUserTimedout -= Client_OnUserTimedOut;
            _client.OnMessageCleared -= Client_OnMessageCleared;
            _client.OnUserBanned -= Client_OnUserBanned;
            _client.OnUserJoined -= Client_OnUserJoined;
            _client.OnUserLeft -= Client_OnUserLeft;
        }
    }
}
