// TwitchStatistics.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Configuration;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Data.Twitch.Manager;
using TwitchScanAPI.DbContext;
using TwitchScanAPI.Global;
using TwitchScanAPI.Models.Enums;
using TwitchScanAPI.Models.Twitch.Channel;
using TwitchScanAPI.Models.Twitch.Chat;
using TwitchScanAPI.Models.Twitch.Statistics;
using TwitchScanAPI.Models.Twitch.User;
using TwitchScanAPI.Services;

namespace TwitchScanAPI.Data.Twitch
{
    public class TwitchStatistics : IDisposable
    {
        // Statistics interval in seconds depending on online status
        private const int StatisticInterval = 1;
        public string ChannelName { get; }
        public int MessageCount { get; private set; }
        public DateTime StartedAt { get; } = DateTime.UtcNow;
        public bool IsOnline;
        private readonly TwitchClientManager _clientManager;
        private readonly StatisticsManager _statisticsManager;
        private readonly ObservedWordsManager _observedWordsManager;
        private readonly UserManager _userManager;
        private readonly NotificationService _notificationService;
        private readonly MongoDbContext _context;
        private readonly Timer _statisticsTimer;
        private readonly TimeSpan _statisticsInterval = TimeSpan.FromSeconds(StatisticInterval);

        private TwitchStatistics(string channelName, TwitchClientManager clientManager,
            NotificationService notificationService, MongoDbContext context)
        {
            ChannelName = channelName;
            _clientManager = clientManager;
            _statisticsManager = new StatisticsManager();
            _observedWordsManager = new ObservedWordsManager();
            _userManager = new UserManager();
            _notificationService = notificationService;
            _context = context;

            // Subscribe to Twitch client events
            SubscribeToClientEvents();

            // Initialize and start the statistics timer
            _statisticsTimer = new Timer(_statisticsInterval.TotalMilliseconds)
            {
                AutoReset = true
            };
            _statisticsTimer.Elapsed += async (_, _) => { await SendStatisticsAsync(); };
            _statisticsTimer.Start();
        }

        public static async Task<TwitchStatistics?> CreateAsync(string channelName, IConfiguration configuration,
            NotificationService notificationService, MongoDbContext context, EmoteService emoteService)
        {
            var clientManager =
                await TwitchClientManager.CreateAsync(channelName, configuration, emoteService);

            return clientManager == null
                ? null
                : new TwitchStatistics(channelName, clientManager, notificationService, context);
        }

        public async Task SaveSnapshotAsync(StatisticsManager? manager = null, DateTime? date = null,
            int? viewCount = null)
        {
            manager ??= _statisticsManager;

            // Try getting the peak viewers from the statistics
            var statistics = manager.GetAllStatistics();
            statistics.TryGetValue("ChannelMetrics", out var value);
            var viewerStatistics = value is ChannelMetrics metrics ? metrics.ViewerStatistics : null;
            // Save the statistics to the database
            var statisticHistory = new StatisticHistory(ChannelName, viewCount ?? viewerStatistics?.PeakViewers ?? 0,
                viewCount ?? viewerStatistics?.AverageViewers ?? 0, MessageCount, statistics)
            {
                Time = date ?? DateTime.UtcNow
            };
            // Check if there is already an entry for this channel within the last 24 hours if so merge the statistics
            
            await _context.StatisticHistory.InsertOneAsync(statisticHistory);

            // Reset the message count and statistics
            MessageCount = 0;
            manager.Reset();
        }

        public async Task RefreshConnectionAsync()
        {
            _clientManager.DisconnectClient();

            try
            {
                await _clientManager.AttemptConnectionAsync();
                Console.WriteLine($"Reconnected to channel '{ChannelName}' with refreshed OAuth token.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Failed to reconnect to channel '{ChannelName}' after refreshing OAuth token: {ex.Message}");
            }
        }

        private void SubscribeToClientEvents()
        {
            _clientManager.OnMessageReceived += ClientManager_OnMessageReceived;
            _clientManager.OnUserJoined += ClientManager_OnUserJoined;
            _clientManager.OnUserLeft += ClientManager_OnUserLeft;
            _clientManager.OnNewSubscriber += ClientManager_OnNewSubscriber;
            _clientManager.OnReSubscriber += ClientManager_OnReSubscriber;
            _clientManager.OnGiftedSubscription += ClientManager_OnGiftedSubscription;
            _clientManager.OnCommunitySubscription += ClientManager_OnCommunitySubscription;
            _clientManager.OnUserBanned += ClientManager_OnUserBanned;
            _clientManager.OnMessageCleared += ClientManager_OnMessageCleared;
            _clientManager.OnUserTimedOut += ClientManager_OnUserTimedOut;
            _clientManager.OnConnectionChanged += ClientManagerOnConnectionChanged;
            _clientManager.OnDisconnected += ClientManagerOnDisconnected;
            _clientManager.OnChannelStateChanged += ClientManagerOnChannelStateChanged;
            _clientManager.OnRaidNotification += ClientManagerOnOnRaidNotification;
        }

        private async void ClientManagerOnDisconnected(object? sender, EventArgs e)
        {
            IsOnline = false;
            _statisticsManager.PropagateEvents = false;
            await SaveSnapshotAsync();
        }

        private async void ClientManagerOnConnectionChanged(object? sender, ChannelInformation channelInformation)
        {
            IsOnline = channelInformation.IsOnline;
            _statisticsManager.PropagateEvents = IsOnline;
            await _notificationService.ReceiveOnlineStatusAsync(new ChannelStatus(ChannelName,
                channelInformation.IsOnline, MessageCount, channelInformation.Viewers, channelInformation.Uptime));
        }

        public void AddTextToObserve(string text)
        {
            _observedWordsManager.AddTextToObserve(text);
        }

        public async Task<ChannelInformation> GetChannelInfoAsync() => await _clientManager.GetChannelInfoAsync();

        public async Task<IDictionary<string, object>> GetStatisticsAsync()
        {
            try
            {
                var channelInfo = await _clientManager.GetChannelInfoAsync();
                await _statisticsManager.Update(channelInfo);

                return _statisticsManager.GetAllStatistics();
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Error fetching statistics for channel '{ChannelName}': {ex.Message} {ex.StackTrace}");
                return new Dictionary<string, object>(); // Return empty if there's an error
            }
        }

        public IEnumerable<string> GetUsers() => _userManager.GetUsers();

        private async Task SendStatisticsAsync()
        {
            var channelInformation = await _clientManager.GetChannelInfoAsync();
            await _notificationService.ReceiveOnlineStatusAsync(new ChannelStatus(ChannelName,
                channelInformation.IsOnline, MessageCount, channelInformation.Viewers, channelInformation.Uptime));
            if (!channelInformation.IsOnline)
                return;

            var statistics = await GetStatisticsAsync();
            await _notificationService.ReceiveStatisticsAsync(ChannelName, statistics);
        }

        #region Event Handlers

        private async void ClientManager_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            var chatMessage = e.ChatMessage;
            var channelMessage = new ChannelMessage(ChannelName, new TwitchChatMessage
            {
                Username = chatMessage.Username,
                Message = chatMessage.Message,
                ColorHex = chatMessage.ColorHex,
                Emotes = e.ChatMessage.EmoteSet.Emotes
                    .Select(em => new TwitchEmote(em.Id, em.Name, em.StartIndex, em.EndIndex))
                    .ToList()
            });

            // Add BTTV and 7TV emotes to the message
            StaticTwitchHelper.AddEmotesToMessage(channelMessage, _clientManager.ExternalChannelEmotes);
            await _notificationService.ReceiveChannelMessageAsync(ChannelName, channelMessage);
            await _notificationService.ReceiveMessageCountAsync(ChannelName, MessageCount);

            // Update message count and statistics if not a bot
            if (!Variables.BotNames.Contains(channelMessage.ChatMessage.Username, StringComparer.OrdinalIgnoreCase))
            {
                MessageCount++;
                await _statisticsManager.Update(channelMessage);
            }

            // Check for observed words
            if (_observedWordsManager.IsMatch(channelMessage.ChatMessage.Message))
            {
                await _notificationService.ReceiveObservedMessageAsync(ChannelName, channelMessage);
            }

            // Check for elevated users
            if (IsElevatedUser(chatMessage))
            {
                await _notificationService.ReceiveElevatedMessageAsync(ChannelName, channelMessage);
            }
        }

        private async void ClientManager_OnUserJoined(object? sender, OnUserJoinedArgs e)
        {
            if (!_userManager.AddUser(e.Username)) return;
            await _statisticsManager.Update(new UserJoined(e.Username));
            await _notificationService.ReceiveUserJoinedAsync(ChannelName, e.Username, e.Channel);
        }

        private async void ClientManager_OnUserLeft(object? sender, OnUserLeftArgs e)
        {
            if (!_userManager.RemoveUser(e.Username)) return;
            await _statisticsManager.Update(new UserLeft(e.Username));
            await _notificationService.ReceiveUserLeftAsync(ChannelName, e.Username);
        }

        private async void ClientManager_OnNewSubscriber(object? sender, OnNewSubscriberArgs e)
        {
            var subscription = new ChannelSubscription(SubscriptionType.New)
            {
                UserName = e.Subscriber.Login,
                DisplayName = e.Subscriber.DisplayName,
                Message = e.Subscriber.ResubMessage,
                SubscriptionPlanName = e.Subscriber.SubscriptionPlanName,
                SubscriptionPlan = e.Subscriber.SubscriptionPlan.ToString(),
                Months = 1,
                MultiMonth = ParseInt(e.Subscriber.MsgParamCumulativeMonths, 1),
            };

            await _statisticsManager.Update(subscription);
            await _notificationService.ReceiveSubscriptionAsync(ChannelName, subscription);
        }

        private async void ClientManager_OnReSubscriber(object? sender, OnReSubscriberArgs e)
        {
            var subscription = new ChannelSubscription(SubscriptionType.Re)
            {
                UserName = e.ReSubscriber.Login,
                DisplayName = e.ReSubscriber.DisplayName,
                Message = e.ReSubscriber.ResubMessage,
                SubscriptionPlanName = e.ReSubscriber.SubscriptionPlanName,
                SubscriptionPlan = e.ReSubscriber.SubscriptionPlan.ToString(),
                Months = ParseInt(e.ReSubscriber.MsgParamStreakMonths, 1),
                MultiMonth = ParseInt(e.ReSubscriber.MsgParamCumulativeMonths, 1)
            };

            await _statisticsManager.Update(subscription);
            await _notificationService.ReceiveSubscriptionAsync(ChannelName, subscription);
        }

        private async void ClientManager_OnGiftedSubscription(object? sender, OnGiftedSubscriptionArgs e)
        {
            var subscription = new ChannelSubscription(SubscriptionType.Gifted)
            {
                UserName = e.GiftedSubscription.Login,
                DisplayName = e.GiftedSubscription.DisplayName,
                RecipientUserName = e.GiftedSubscription.MsgParamRecipientUserName,
                RecipientDisplayName = e.GiftedSubscription.MsgParamRecipientDisplayName,
                SubscriptionPlanName = e.GiftedSubscription.MsgParamSubPlanName,
                SubscriptionPlan = e.GiftedSubscription.MsgParamSubPlan.ToString(),
                Months = ParseInt(e.GiftedSubscription.MsgParamMultiMonthGiftDuration, 1),
                MultiMonth = ParseInt(e.GiftedSubscription.MsgParamMonths, 1),
                Message = e.GiftedSubscription.SystemMsg,
                GiftedSubscriptionPlan = e.GiftedSubscription.MsgParamSubPlanName
            };

            await _statisticsManager.Update(subscription);
            await _notificationService.ReceiveSubscriptionAsync(ChannelName, subscription);
        }

        private async void ClientManager_OnCommunitySubscription(object? sender, OnCommunitySubscriptionArgs e)
        {
            var subscription = new ChannelSubscription(SubscriptionType.Community)
            {
                UserName = e.GiftedSubscription.Login,
                DisplayName = e.GiftedSubscription.DisplayName,
                GiftedSubscriptionCount = e.GiftedSubscription.MsgParamMassGiftCount,
                GiftedSubscriptionPlan = e.GiftedSubscription.MsgParamSubPlan.ToString(),
                MultiMonth = ParseInt(e.GiftedSubscription.MsgParamMultiMonthGiftDuration, 1),
            };

            await _statisticsManager.Update(subscription);
            await _notificationService.ReceiveSubscriptionAsync(ChannelName, subscription);
        }

        private async void ClientManager_OnUserBanned(object? sender, OnUserBannedArgs e)
        {
            var bannedUser = new UserBanned(e.UserBan.Username, e.UserBan.BanReason);

            await _statisticsManager.Update(bannedUser);
            await _notificationService.ReceiveBannedUserAsync(ChannelName, bannedUser);
        }

        private async void ClientManager_OnMessageCleared(object? sender, OnMessageClearedArgs e)
        {
            var clearedMessage = new ClearedMessage
            {
                Message = e.Message,
                TargetMessageId = e.TargetMessageId,
                TmiSentTs = e.TmiSentTs,
            };

            await _statisticsManager.Update(clearedMessage);
            await _notificationService.ReceiveClearedMessageAsync(ChannelName, clearedMessage);
        }

        private async void ClientManager_OnUserTimedOut(object? sender, OnUserTimedoutArgs e)
        {
            var timedOutUser = new UserTimedOut
            {
                Username = e.UserTimeout.Username,
                TimeoutReason = e.UserTimeout.TimeoutReason,
                TimeoutDuration = e.UserTimeout.TimeoutDuration,
            };

            await _statisticsManager.Update(timedOutUser);
            await _notificationService.ReceiveTimedOutUserAsync(ChannelName, timedOutUser);
        }

        private async void ClientManagerOnChannelStateChanged(object? sender, OnChannelStateChangedArgs e)
        {
            await _statisticsManager.Update(e.ChannelState);
        }

        private async void ClientManagerOnOnRaidNotification(object? sender, OnRaidNotificationArgs e)
        {
            await _statisticsManager.Update(e.RaidNotification);
        }

        #endregion

        #region Helper Methods

        private static int ParseInt(string? value, int defaultValue) =>
            int.TryParse(value, out var result) ? result : defaultValue;

        private static bool IsElevatedUser(ChatMessage message) =>
            message.IsModerator || message.IsPartner || message.IsStaff || message.IsVip;

        #endregion

        public void Dispose()
        {
            _clientManager.Dispose();
            _statisticsTimer.Stop();
            _statisticsTimer.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}