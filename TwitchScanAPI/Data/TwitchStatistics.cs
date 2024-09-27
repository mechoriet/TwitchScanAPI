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
using TwitchScanAPI.Models.Twitch;

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

        // Statistics
        public Statistics.Base.Statistics Statistics { get; }

        // Words to observe
        private readonly HashSet<string> _wordsToObserve = new(StringComparer.OrdinalIgnoreCase);
        private Regex? _observePatternRegex;

        public TwitchStatistics(string channelName, IHubContext<TwitchHub, ITwitchHub> hubContext, IConfiguration configuration)
        {
            ChannelName = channelName;
            _hubContext = hubContext;
            _configuration = configuration;
            _client = InitializeClient();
            Statistics = new Statistics.Base.Statistics();
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
            var credentials = new ConnectionCredentials(twitchChatName, oauth);
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
            client.OnRaidNotification += Client_OnRaid;
            client.OnBeingHosted += ClientOnOnBeingHosted;

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

        // Event Handlers

        private async void Client_OnUserLeft(object? sender, OnUserLeftArgs e)
        {
            Users.TryRemove(e.Username, out _);
            await _hubContext.Clients.Group(ChannelName).ReceiveUserLeft(e.Username);
        }

        private async void Client_OnUserJoined(object? sender, OnUserJoinedArgs e)
        {
            Users.TryAdd(e.Username, e.Channel);
            await _hubContext.Clients.Group(ChannelName).ReceiveUserJoined(e.Username, e.Channel);
        }

        private async void Client_OnNewSubscriber(object? sender, OnNewSubscriberArgs e)
        {
            var subscription = new Subscription
            {
                Type = SubscriptionType.New,
                Time = DateTime.UtcNow,
                UserName = e.Subscriber.Login,
                DisplayName = e.Subscriber.DisplayName,
                Message = e.Subscriber.ResubMessage,
                SubscriptionPlanName = e.Subscriber.SubscriptionPlanName,
                SubscriptionPlan = e.Subscriber.SubscriptionPlan.ToString(),
            };
            Statistics.Update(subscription);
            await _hubContext.Clients.Group(ChannelName).ReceiveSubscription(subscription);
        }

        private async void Client_OnReSubscriber(object? sender, OnReSubscriberArgs e)
        {
            var subscription = new Subscription
            {
                Type = SubscriptionType.Re,
                Time = DateTime.UtcNow,
                UserName = e.ReSubscriber.Login,
                DisplayName = e.ReSubscriber.DisplayName,
                Message = e.ReSubscriber.ResubMessage,
                SubscriptionPlanName = e.ReSubscriber.SubscriptionPlanName,
                SubscriptionPlan = e.ReSubscriber.SubscriptionPlan.ToString(),
                Months = e.ReSubscriber.Months,
            };
            Statistics.Update(subscription);
            await _hubContext.Clients.Group(ChannelName).ReceiveSubscription(subscription);
        }

        private async void Client_OnGiftedSubscription(object? sender, OnGiftedSubscriptionArgs e)
        {
            var subscription = new Subscription
            {
                Type = SubscriptionType.Gifted,
                Time = DateTime.UtcNow,
                UserName = e.GiftedSubscription.Login,
                DisplayName = e.GiftedSubscription.DisplayName,
                RecipientUserName = e.GiftedSubscription.MsgParamRecipientUserName,
                RecipientDisplayName = e.GiftedSubscription.MsgParamRecipientDisplayName,
                SubscriptionPlanName = e.GiftedSubscription.MsgParamSubPlanName,
                SubscriptionPlan = e.GiftedSubscription.MsgParamSubPlan.ToString(),
                Months = int.TryParse(e.GiftedSubscription.MsgParamMonths, out var months) ? months : 1,
                Message = e.GiftedSubscription.SystemMsg,
                GiftedSubscriptionCount = int.TryParse(e.GiftedSubscription.MsgParamMultiMonthGiftDuration, out var count) ? count : 1,
                GiftedSubscriptionPlan = e.GiftedSubscription.MsgParamSubPlanName
            };
            await _hubContext.Clients.Group(ChannelName).ReceiveSubscription(subscription);
        }
        
        private async void Client_OnRaid(object? sender, OnRaidNotificationArgs e)
        {
            var raidEvent = new RaidEvent
            {
                Raider = e.RaidNotification.MsgParamDisplayName,
                ViewerCount = int.TryParse(e.RaidNotification.MsgParamViewerCount, out var count) ? count : 0,
                Time = DateTime.UtcNow
            };

            Statistics.Update(raidEvent);
            await _hubContext.Clients.Group(ChannelName).ReceiveRaidEvent(raidEvent);
        }

        private async void ClientOnOnBeingHosted(object? sender, OnBeingHostedArgs e)
        {
            var hostEvent = new HostEvent
            {
                Hoster = e.BeingHostedNotification.HostedByChannel,
                ViewerCount = e.BeingHostedNotification.Viewers,
                Time = DateTime.UtcNow
            };

            Statistics.Update(hostEvent);
            await _hubContext.Clients.Group(ChannelName).ReceiveHostEvent(hostEvent);
        }

        private async void Client_OnCommunitySubscription(object? sender, OnCommunitySubscriptionArgs e)
        {
            var subscription = new Subscription
            {
                Type = SubscriptionType.Community,
                Time = DateTime.UtcNow,
                UserName = e.GiftedSubscription.Login,
                DisplayName = e.GiftedSubscription.DisplayName,
                GiftedSubscriptionCount = e.GiftedSubscription.MsgParamMassGiftCount,
                GiftedSubscriptionPlan = e.GiftedSubscription.MsgParamSubPlan.ToString()
            };
            
            Statistics.Update(subscription);
            await _hubContext.Clients.Group(ChannelName).ReceiveSubscription(subscription);
        }

        private async void Client_OnUserBanned(object? sender, OnUserBannedArgs e)
        {
            var bannedUser = new BannedUser
            {
                Username = e.UserBan.Username,
                BanReason = e.UserBan.BanReason,
                Time = DateTime.UtcNow
            };
            
            Statistics.Update(bannedUser);
            await _hubContext.Clients.Group(ChannelName).ReceiveBannedUser(bannedUser);
        }

        private async void Client_OnMessageCleared(object? sender, OnMessageClearedArgs e)
        {
            var clearedMessage = new ClearedMessage
            {
                Message = e.Message,
                TargetMessageId = e.TargetMessageId,
                TmiSentTs = e.TmiSentTs,
                Time = DateTime.UtcNow
            };
            
            Statistics.Update(clearedMessage);
            await _hubContext.Clients.Group(ChannelName).ReceiveClearedMessage(clearedMessage);
        }

        private async void Client_OnUserTimedOut(object? sender, OnUserTimedoutArgs e)
        {
            var timedOutUser = new TimedOutUser
            {
                Username = e.UserTimeout.Username,
                TimeoutReason = e.UserTimeout.TimeoutReason,
                TimeoutDuration = e.UserTimeout.TimeoutDuration,
                Time = DateTime.UtcNow
            };
            
            Statistics.Update(timedOutUser);
            await _hubContext.Clients.Group(ChannelName).ReceiveTimedOutUser(timedOutUser);
        }

        private async void Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            var channelMessage = new ChannelMessage(ChannelName, e.ChatMessage);
            await _hubContext.Clients.Group(ChannelName).ReceiveChannelMessage(channelMessage);

            // Update statistics
            Statistics.Update(channelMessage);

            // Check for observed words
            if (_observePatternRegex != null && _observePatternRegex.IsMatch(e.ChatMessage.Message))
            {
                await _hubContext.Clients.Group(ChannelName).ReceiveObservedMessage(channelMessage);
            }
            if ((!e.ChatMessage.IsModerator && !e.ChatMessage.IsPartner && !e.ChatMessage.IsStaff &&
                 !e.ChatMessage.IsVip) ||
                Variables.BotNames.Contains(e.ChatMessage.DisplayName, StringComparer.OrdinalIgnoreCase)) return;

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
