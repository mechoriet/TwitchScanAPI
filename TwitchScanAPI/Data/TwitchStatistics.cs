// TwitchStatistics.cs
#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;
using TwitchScanAPI.Global;
using TwitchScanAPI.Hubs;
using TwitchScanAPI.Models.Enums;
using TwitchScanAPI.Models.Twitch.Channel;
using TwitchScanAPI.Models.Twitch.Chat;
using TwitchScanAPI.Models.Twitch.User;

namespace TwitchScanAPI.Data
{
    public class TwitchStatistics : IDisposable
    {
        // Configuration
        private readonly TwitchClient _client;
        private readonly TwitchPubSub _pubSubClient = new();
        private readonly IHubContext<TwitchHub, ITwitchHub> _hubContext;
        private readonly IConfiguration _configuration;

        // Channel Information
        public string ChannelName { get; }
        public int MessageCount { get; private set; }
        public ConcurrentDictionary<string, string> Users { get; } = new(StringComparer.OrdinalIgnoreCase);

        // Statistics
        public Statistics.Base.Statistics Statistics { get; }

        // Words to observe
        private readonly HashSet<string> _wordsToObserve = new(StringComparer.OrdinalIgnoreCase);
        private Regex? _observePatternRegex;
        
        // Timer for regularly sending statistics
        private readonly Timer? _statisticsTimer;
        private readonly TimeSpan _sendInterval = TimeSpan.FromSeconds(60);

        public TwitchStatistics(string channelName, IHubContext<TwitchHub, ITwitchHub> hubContext, IConfiguration configuration)
        {
            ChannelName = channelName;
            _hubContext = hubContext;
            _configuration = configuration;
            _client = InitializeClient();
            Statistics = new Statistics.Base.Statistics();
            ConnectClient();
            // Initialize the timer to send statistics at regular intervals
            _statisticsTimer = new Timer(_sendInterval.TotalMilliseconds);
            _statisticsTimer.Elapsed += async (_, _) => await SendStatistics();
            _statisticsTimer.AutoReset = true;  // Ensures the timer will keep triggering every hour
            _statisticsTimer.Start();
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
            var client = new TwitchClient(customClient)
            {
                AutoReListenOnException = true
            };
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
            client.OnBeingHosted += ClientOnBeingHosted;
            
            // Connect to the PubSub client
            _pubSubClient.OnViewCount += PubSubClientOnViewCount;
            _pubSubClient.OnStreamUp += onStreamUp;
            _pubSubClient.OnStreamDown += onStreamDown;
            _pubSubClient.OnPubSubServiceConnected += PubSubClientOnOnPubSubServiceConnected;
            
            _pubSubClient.ListenToVideoPlayback(ChannelName);
            _pubSubClient.Connect();

            return client;
        }

        private void PubSubClientOnOnPubSubServiceConnected(object? sender, EventArgs e)
        {
            _pubSubClient.SendTopics(_configuration.GetValue<string>(Variables.TwitchOauthKey));
        }

        private void PubSubClientOnViewCount(object? sender, OnViewCountArgs e)
        {
            var viewCount = new ChannelViewCount
            {
                ViewerCount = e.Viewers,
                StreamTime = e.ServerTime
            };
            Statistics.Update(viewCount);
        }
        
        private void onStreamUp(object? sender, OnStreamUpArgs e)
        {
            
        }
        
        private void onStreamDown(object? sender, OnStreamDownArgs e)
        {
            
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
        
        private async Task SendStatistics()
        {
            var statistics = Statistics.GetAllStatistics();
            if (statistics == null) return;
            await _hubContext.Clients.Group(ChannelName).ReceiveStatistics(statistics);
        }

        // Event Handlers

        private async void Client_OnUserLeft(object? sender, OnUserLeftArgs e)
        {
            Users.TryRemove(e.Username, out _);
            
            Statistics.Update(new UserLeft(e.Username));
            await _hubContext.Clients.Group(ChannelName).ReceiveUserLeft(e.Username);
        }

        private async void Client_OnUserJoined(object? sender, OnUserJoinedArgs e)
        {
            Users.TryAdd(e.Username, e.Channel);
            
            Statistics.Update(new UserJoined(e.Username));
            await _hubContext.Clients.Group(ChannelName).ReceiveUserJoined(e.Username, e.Channel);
        }

        private async void Client_OnNewSubscriber(object? sender, OnNewSubscriberArgs e)
        {
            var subscription = new ChannelSubscription(SubscriptionType.New)
            {
                UserName = e.Subscriber.Login,
                DisplayName = e.Subscriber.DisplayName,
                Message = e.Subscriber.ResubMessage,
                SubscriptionPlanName = e.Subscriber.SubscriptionPlanName,
                SubscriptionPlan = e.Subscriber.SubscriptionPlan.ToString(),
                Months = 1,
                MultiMonth = int.TryParse(e.Subscriber.MsgParamCumulativeMonths, out var multi) ? multi : 1,
            };
            
            Statistics.Update(subscription);
            await _hubContext.Clients.Group(ChannelName).ReceiveSubscription(subscription);
        }

        private async void Client_OnReSubscriber(object? sender, OnReSubscriberArgs e)
        {
            var subscription = new ChannelSubscription(SubscriptionType.Re)
            {
                UserName = e.ReSubscriber.Login,
                DisplayName = e.ReSubscriber.DisplayName,
                Message = e.ReSubscriber.ResubMessage,
                SubscriptionPlanName = e.ReSubscriber.SubscriptionPlanName,
                SubscriptionPlan = e.ReSubscriber.SubscriptionPlan.ToString(),
                MultiMonth = int.TryParse(e.ReSubscriber.MsgParamCumulativeMonths, out var multi) ? multi : 1,
                Months = int.TryParse(e.ReSubscriber.MsgParamStreakMonths, out var months) ? months : 1
            };
            
            Statistics.Update(subscription);
            await _hubContext.Clients.Group(ChannelName).ReceiveSubscription(subscription);
        }

        private async void Client_OnGiftedSubscription(object? sender, OnGiftedSubscriptionArgs e)
        {
            var subscription = new ChannelSubscription(SubscriptionType.Gifted)
            {
                UserName = e.GiftedSubscription.Login,
                DisplayName = e.GiftedSubscription.DisplayName,
                RecipientUserName = e.GiftedSubscription.MsgParamRecipientUserName,
                RecipientDisplayName = e.GiftedSubscription.MsgParamRecipientDisplayName,
                SubscriptionPlanName = e.GiftedSubscription.MsgParamSubPlanName,
                SubscriptionPlan = e.GiftedSubscription.MsgParamSubPlan.ToString(),
                Months = int.TryParse(e.GiftedSubscription.MsgParamMonths, out var months) ? months : 1,
                MultiMonth = int.TryParse(e.GiftedSubscription.MsgParamMultiMonthGiftDuration, out var multiMonth) ? multiMonth : 1,
                Message = e.GiftedSubscription.SystemMsg,
                GiftedSubscriptionPlan = e.GiftedSubscription.MsgParamSubPlanName
            };
            
            Statistics.Update(subscription);
            await _hubContext.Clients.Group(ChannelName).ReceiveSubscription(subscription);
        }

        private async void Client_OnCommunitySubscription(object? sender, OnCommunitySubscriptionArgs e)
        {
            var subscription = new ChannelSubscription(SubscriptionType.Community)
            {
                UserName = e.GiftedSubscription.Login,
                DisplayName = e.GiftedSubscription.DisplayName,
                GiftedSubscriptionCount = e.GiftedSubscription.MsgParamMassGiftCount,
                GiftedSubscriptionPlan = e.GiftedSubscription.MsgParamSubPlan.ToString(),
                MultiMonth = int.TryParse(e.GiftedSubscription.MsgParamMultiMonthGiftDuration, out var multiMonth) ? multiMonth : 1,
            };
            
            Statistics.Update(subscription);
            await _hubContext.Clients.Group(ChannelName).ReceiveSubscription(subscription);
        }
        
        private async void Client_OnRaid(object? sender, OnRaidNotificationArgs e)
        {
            var raidEvent = new ChannelRaid
            {
                Raider = e.RaidNotification.MsgParamDisplayName,
                ViewerCount = int.TryParse(e.RaidNotification.MsgParamViewerCount, out var count) ? count : 0
            };

            Statistics.Update(raidEvent);
            await _hubContext.Clients.Group(ChannelName).ReceiveRaidEvent(raidEvent);
        }

        private async void ClientOnBeingHosted(object? sender, OnBeingHostedArgs e)
        {
            var hostEvent = new ChannelHost
            {
                Hoster = e.BeingHostedNotification.HostedByChannel,
                ViewerCount = e.BeingHostedNotification.Viewers
            };

            Statistics.Update(hostEvent);
            await _hubContext.Clients.Group(ChannelName).ReceiveHostEvent(hostEvent);
        }

        private async void Client_OnUserBanned(object? sender, OnUserBannedArgs e)
        {
            var bannedUser = new UserBanned(e.UserBan.Username, e.UserBan.BanReason);
            
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
            };
            
            Statistics.Update(clearedMessage);
            await _hubContext.Clients.Group(ChannelName).ReceiveClearedMessage(clearedMessage);
        }

        private async void Client_OnUserTimedOut(object? sender, OnUserTimedoutArgs e)
        {
            var timedOutUser = new UserTimedOut
            {
                Username = e.UserTimeout.Username,
                TimeoutReason = e.UserTimeout.TimeoutReason,
                TimeoutDuration = e.UserTimeout.TimeoutDuration,
            };
            
            Statistics.Update(timedOutUser);
            await _hubContext.Clients.Group(ChannelName).ReceiveTimedOutUser(timedOutUser);
        }

        private async void Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            var channelMessage = new ChannelMessage(ChannelName, e.ChatMessage);
            await _hubContext.Clients.Group(ChannelName).ReceiveChannelMessage(channelMessage);

            // Update statistics
            if (!Variables.BotNames.Contains(e.ChatMessage.DisplayName.ToLower(), StringComparer.OrdinalIgnoreCase))
            {
                MessageCount++;
                Statistics.Update(channelMessage);
            }

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
            GC.SuppressFinalize(this);
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
            
            _statisticsTimer?.Stop();
            _statisticsTimer?.Dispose();
        }
    }
}
