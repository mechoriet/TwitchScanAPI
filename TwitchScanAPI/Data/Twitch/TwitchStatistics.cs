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
using TwitchScanAPI.Global;
using TwitchScanAPI.Models.Enums;
using TwitchScanAPI.Models.Twitch.Channel;
using TwitchScanAPI.Models.Twitch.Chat;
using TwitchScanAPI.Models.Twitch.User;
using TwitchScanAPI.Services;

namespace TwitchScanAPI.Data.Twitch
{
    public class TwitchStatistics : IDisposable
    {
        public string ChannelName { get; }
        public int MessageCount { get; private set; }
        public DateTime StartedAt { get; } = DateTime.Now;
        public bool IsConnected => _clientManager.IsConnected;

        private readonly TwitchClientManager _clientManager;
        private readonly StatisticsManager _statisticsManager;
        private readonly ObservedWordsManager _observedWordsManager;
        private readonly UserManager _userManager;
        private readonly NotificationService _notificationService;
        private readonly Timer _statisticsTimer;

        public TwitchStatistics(string channelName,
            IConfiguration configuration, NotificationService notificationService)
        {
            ChannelName = channelName;
            _notificationService = notificationService;

            _clientManager = new TwitchClientManager(channelName, configuration);
            _statisticsManager = new StatisticsManager();
            _observedWordsManager = new ObservedWordsManager();
            _userManager = new UserManager();

            // Subscribe to Twitch client events
            SubscribeToClientEvents();

            // Initialize and start the statistics timer
            _statisticsTimer = new Timer(TimeSpan.FromSeconds(60).TotalMilliseconds)
            {
                AutoReset = true
            };
            _statisticsTimer.Elapsed += async (_, _) => await SendStatisticsAsync();
            _statisticsTimer.Start();

            // Start the Twitch client connection
            _ = _clientManager.AttemptConnectionAsync();
        }

        public async Task RefreshConnectionAsync()
        {
            _clientManager.DisconnectClient();

            // Attempt to reconnect using the updated OAuth token
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
            _clientManager.OnRaidNotification += ClientManager_OnRaidNotification;
            _clientManager.OnUserBanned += ClientManager_OnUserBanned;
            _clientManager.OnMessageCleared += ClientManager_OnMessageCleared;
            _clientManager.OnUserTimedOut += ClientManager_OnUserTimedOut;
            _clientManager.OnConnected += async (_, isOnline) => await _notificationService.ReceiveOnlineStatusAsync(ChannelName, isOnline);
        }

        public void AddTextToObserve(string text)
        {
            _observedWordsManager.AddTextToObserve(text);
        }

        public async Task<IDictionary<string, object>> GetStatisticsAsync()
        {
            try
            {
                // Fetch the stream data using the Twitch API
                var streams =
                    await _clientManager.Api.Helix.Streams.GetStreamsAsync(userLogins: new List<string>
                        { ChannelName });
                var stream = streams?.Streams.FirstOrDefault();

                if (stream == null) return _statisticsManager.GetAllStatistics();
                // Create a new ChannelInformation object based on the stream data
                var channelInfo = new ChannelInformation
                {
                    Viewers = stream.ViewerCount,
                    Title = stream.Title,
                    Game = stream.GameName,
                    Uptime = stream.StartedAt,
                    Thumbnail = stream.ThumbnailUrl,
                    StreamType = stream.Type
                };
                _statisticsManager.Update(channelInfo);

                return _statisticsManager.GetAllStatistics();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching statistics for channel '{ChannelName}': {ex.Message}");
                return new Dictionary<string, object>(); // Return empty if there's an error
            }
        }

        public IEnumerable<string> GetUsers() => _userManager.GetUsers();

        private async Task SendStatisticsAsync()
        {
            if (!IsConnected)
                return;

            var statistics = await GetStatisticsAsync();
            await _notificationService.ReceiveStatisticsAsync(ChannelName, statistics);
            await _notificationService.ReceiveOnlineStatusAsync(ChannelName, IsConnected);
        }

        #region Event Handlers

        private async void ClientManager_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            var chatMessage = e.ChatMessage;
            var channelMessage = new ChannelMessage(ChannelName, chatMessage);
            await _notificationService.ReceiveChannelMessageAsync(ChannelName, channelMessage);

            // Update message count and statistics if not a bot
            if (!Variables.BotNames.Contains(chatMessage.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                MessageCount++;
                _statisticsManager.Update(channelMessage);
            }

            // Check for observed words
            if (_observedWordsManager.IsMatch(chatMessage.Message))
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
            _statisticsManager.Update(new UserJoined(e.Username));
            await _notificationService.ReceiveUserJoinedAsync(ChannelName, e.Username, e.Channel);
        }

        private async void ClientManager_OnUserLeft(object? sender, OnUserLeftArgs e)
        {
            if (_userManager.RemoveUser(e.Username))
            {
                _statisticsManager.Update(new UserLeft(e.Username));
                await _notificationService.ReceiveUserLeftAsync(ChannelName, e.Username);
            }
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

            _statisticsManager.Update(subscription);
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

            _statisticsManager.Update(subscription);
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
                Months = ParseInt(e.GiftedSubscription.MsgParamMonths, 1),
                MultiMonth = ParseInt(e.GiftedSubscription.MsgParamMultiMonthGiftDuration, 1),
                Message = e.GiftedSubscription.SystemMsg,
                GiftedSubscriptionPlan = e.GiftedSubscription.MsgParamSubPlanName
            };

            _statisticsManager.Update(subscription);
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

            _statisticsManager.Update(subscription);
            await _notificationService.ReceiveSubscriptionAsync(ChannelName, subscription);
        }

        private async void ClientManager_OnRaidNotification(object? sender, OnRaidNotificationArgs e)
        {
            var raidEvent = new ChannelRaid
            {
                Raider = e.RaidNotification.MsgParamDisplayName,
                ViewerCount = ParseInt(e.RaidNotification.MsgParamViewerCount, 0)
            };

            _statisticsManager.Update(raidEvent);
            await _notificationService.ReceiveRaidEventAsync(ChannelName, raidEvent);
        }

        private async void ClientManager_OnUserBanned(object? sender, OnUserBannedArgs e)
        {
            var bannedUser = new UserBanned(e.UserBan.Username, e.UserBan.BanReason);

            _statisticsManager.Update(bannedUser);
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

            _statisticsManager.Update(clearedMessage);
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

            _statisticsManager.Update(timedOutUser);
            await _notificationService.ReceiveTimedOutUserAsync(ChannelName, timedOutUser);
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