using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.PubSub.Events;
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
using Timer = System.Timers.Timer;

namespace TwitchScanAPI.Data.Twitch
{
    public class TwitchStatistics : IDisposable
    {
        // Statistics interval in seconds depending on online status
        private const int StatisticInterval = 1;
        private readonly TwitchClientManager _clientManager;
        private readonly MongoDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly ObservedWordsManager _observedWordsManager;
        private readonly TimeSpan _statisticsInterval = TimeSpan.FromSeconds(StatisticInterval);
        private readonly StatisticsManager _statisticsManager;
        private readonly Timer _statisticsTimer;
        private readonly UserManager _userManager;
        private bool _isProcessingOfflineStatus;
        private bool _disposed;
        private readonly Lock _lockObject = new();
        public bool IsOnline { get; private set; }

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
            _statisticsTimer.Elapsed += async (_, _) =>
            {
                if (!_disposed)
                    await SendStatisticsAsync();
            };
            _statisticsTimer.Start();
        }

        public string ChannelName { get; }
        public int MessageCount { get; private set; }
        public DateTime StartedAt { get; } = DateTime.UtcNow;

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _statisticsManager.Reset();
            _clientManager.Dispose();
            _statisticsTimer.Stop();
            _statisticsTimer.Dispose();
            GC.SuppressFinalize(this);
        }

        public static async Task<TwitchStatistics?> CreateAsync(string channelName,
            TwitchManagerFactory clientManagerFactory,
            NotificationService notificationService, MongoDbContext context)
        {
            var clientManager =
                await clientManagerFactory.GetOrCreateClientManagerAsync(channelName);

            return clientManager == null
                ? null
                : new TwitchStatistics(channelName, clientManager, notificationService, context);
        }

        public async Task SaveSnapshotAsync(StatisticsManager? manager = null, DateTime? date = null,
            int? viewCount = null)
        {
            if (_disposed) return;

            manager ??= _statisticsManager;

            // Try getting the peak viewers from the statistics
            try
            {
                var statistics = manager.GetAllStatistics();
                statistics.TryGetValue("ChannelMetrics", out var value);
                var viewerStatistics = value is ChannelMetrics metrics ? metrics.ViewerStatistics : null;

                // Save the statistics to the database
                var statisticHistory = new StatisticHistory(ChannelName,
                    viewCount ?? viewerStatistics?.PeakViewers ?? 0,
                    viewCount ?? viewerStatistics?.AverageViewers ?? 0, MessageCount, statistics)
                {
                    Time = date ?? DateTime.UtcNow
                };

                await _context.StatisticHistory.InsertOneAsync(statisticHistory);
                Console.WriteLine($"Saved snapshot for channel '{ChannelName}' with {MessageCount} messages");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving snapshot for channel '{ChannelName}': {ex.Message} {ex.StackTrace}");
            }

            // Reset the message count and statistics
            MessageCount = 0;
            manager.Reset();
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
            _clientManager.OnCommercial += ClientManager_OnCommercial;
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
            try
            {
                await HandleChannelOfflineAsync();
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error handling channel disconnect for '{ChannelName}': {err.Message}");
            }
        }

        private async Task HandleChannelOfflineAsync()
        {
            // Use a lock to prevent multiple simultaneous calls from processing offline status
            lock (_lockObject)
            {
                if (_isProcessingOfflineStatus) return;
                _isProcessingOfflineStatus = true;
            }

            try
            {
                // Only save data if we were previously online
                if (IsOnline)
                {
                    IsOnline = false;
                    _statisticsManager.PropagateEvents = false;
                    await SaveSnapshotAsync();
                    Console.WriteLine($"Channel '{ChannelName}' went offline. Snapshot saved.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling offline status for '{ChannelName}': {ex.Message}");
            }
            finally
            {
                lock (_lockObject)
                {
                    _isProcessingOfflineStatus = false;
                }
            }
        }

        private async void ClientManagerOnConnectionChanged(object? sender, ChannelInformation channelInformation)
        {
            try
            {
                if (_disposed) return;

                var wasOnline = IsOnline;
                IsOnline = channelInformation.IsOnline;

                // Only change propagation when the status actually changes
                if (wasOnline != IsOnline)
                {
                    _statisticsManager.PropagateEvents = IsOnline;

                    switch (IsOnline)
                    {
                        // If the channel just went offline, save a snapshot
                        case false when wasOnline:
                            await HandleChannelOfflineAsync();
                            break;
                        // If the channel just came online, log it
                        case true when !wasOnline:
                            Console.WriteLine($"Channel '{ChannelName}' is now online.");
                            break;
                    }
                }

                await _notificationService.ReceiveOnlineStatusAsync(new ChannelStatus(ChannelName,
                    channelInformation.IsOnline, MessageCount, channelInformation.Viewers, channelInformation.Uptime));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error handling connection change for '{ChannelName}': {e.Message}");
            }
        }

        public void AddTextToObserve(string text)
        {
            _observedWordsManager.AddTextToObserve(text);
        }

        public async Task<ChannelInformation> GetChannelInfoAsync()
        {
            return await _clientManager.GetChannelInfoAsync();
        }

        public async Task<Dictionary<string, object?>> GetStatisticsAsync()
        {
            if (_disposed) return new Dictionary<string, object?>();

            try
            {
                var channelInfo = await _clientManager.GetChannelInfoAsync();

                // If the channel is offline according to the channel info, but we think it's online,
                // update our status
                if (!channelInfo.IsOnline && IsOnline)
                {
                    await HandleChannelOfflineAsync();
                }

                await _statisticsManager.Update(channelInfo);

                return _statisticsManager.GetAllStatistics();
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Error fetching statistics for channel '{ChannelName}': {ex.Message} {ex.StackTrace}");
                return new Dictionary<string, object?>(); // Return empty if there's an error
            }
        }

        public IEnumerable<string> GetUsers()
        {
            return _userManager.GetUsers();
        }

        public List<ChatHistory> GetChatHistory(string username)
        {
            return _statisticsManager.GetChatHistory(username);
        }

        private async Task SendStatisticsAsync()
        {
            if (_disposed) return;

            try
            {
                var channelInformation = await _clientManager.GetChannelInfoAsync();

                // Double-check if the channel status changed
                if (IsOnline != channelInformation.IsOnline)
                {
                    switch (IsOnline)
                    {
                        case true when !channelInformation.IsOnline:
                            await HandleChannelOfflineAsync();
                            break;
                        case false when channelInformation.IsOnline:
                            IsOnline = true;
                            _statisticsManager.PropagateEvents = true;
                            Console.WriteLine($"Channel '{ChannelName}' is now online.");
                            break;
                    }
                }

                await _notificationService.ReceiveOnlineStatusAsync(new ChannelStatus(ChannelName,
                    channelInformation.IsOnline, MessageCount, channelInformation.Viewers, channelInformation.Uptime));

                if (!channelInformation.IsOnline)
                    return;

                var statistics = await GetStatisticsAsync();
                await _notificationService.ReceiveStatisticsAsync(ChannelName, statistics);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendStatisticsAsync for '{ChannelName}': {ex.Message}");
            }
        }

        #region Event Handlers

        private async void ClientManager_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            try
            {
                if (_disposed) return;

                var chatMessage = e.ChatMessage;
                var channelMessage = new ChannelMessage(ChannelName, new TwitchChatMessage
                {
                    Username = chatMessage.Username,
                    Message = chatMessage.Message,
                    ColorHex = chatMessage.ColorHex,
                    Bits = chatMessage.Bits,
                    BitsInDollars = chatMessage.BitsInDollars,
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
                    await _notificationService.ReceiveObservedMessageAsync(ChannelName, channelMessage);

                // Check for elevated users
                if (IsElevatedUser(chatMessage))
                    await _notificationService.ReceiveElevatedMessageAsync(ChannelName, channelMessage);
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing message for '{ChannelName}': {err.Message}");
            }
        }

        private async void ClientManager_OnUserJoined(object? sender, OnUserJoinedArgs e)
        {
            try
            {
                if (_disposed) return;

                if (!_userManager.AddUser(e.Username)) return;
                await _statisticsManager.Update(new UserJoined(e.Username));
                await _notificationService.ReceiveUserJoinedAsync(ChannelName, e.Username, e.Channel);
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing user join for '{ChannelName}': {err.Message}");
            }
        }

        private async void ClientManager_OnUserLeft(object? sender, OnUserLeftArgs e)
        {
            try
            {
                if (_disposed) return;

                if (!_userManager.RemoveUser(e.Username)) return;
                await _statisticsManager.Update(new UserLeft(e.Username));
                await _notificationService.ReceiveUserLeftAsync(ChannelName, e.Username);
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing user leave for '{ChannelName}': {err.Message}");
            }
        }

        private async void ClientManager_OnNewSubscriber(object? sender, OnNewSubscriberArgs e)
        {
            try
            {
                if (_disposed) return;

                var subscription = new ChannelSubscription(SubscriptionType.New)
                {
                    UserName = e.Subscriber.Login,
                    DisplayName = e.Subscriber.DisplayName,
                    Message = e.Subscriber.ResubMessage,
                    SubscriptionPlanName = e.Subscriber.SubscriptionPlanName,
                    SubscriptionPlan = e.Subscriber.SubscriptionPlan.ToString(),
                    Months = 1,
                    MultiMonth = ParseInt(e.Subscriber.MsgParamCumulativeMonths, 1)
                };

                await _statisticsManager.Update(subscription);
                await _notificationService.ReceiveSubscriptionAsync(ChannelName, subscription);
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing new subscriber for '{ChannelName}': {err.Message}");
            }
        }

        private async void ClientManager_OnReSubscriber(object? sender, OnReSubscriberArgs e)
        {
            try
            {
                if (_disposed) return;

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
            catch (Exception err)
            {
                Console.WriteLine($"Error processing re-subscriber for '{ChannelName}': {err.Message}");
            }
        }

        private async void ClientManager_OnGiftedSubscription(object? sender, OnGiftedSubscriptionArgs e)
        {
            try
            {
                if (_disposed) return;

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
            catch (Exception err)
            {
                Console.WriteLine($"Error processing gifted subscription for '{ChannelName}': {err.Message}");
            }
        }

        private async void ClientManager_OnCommercial(object? sender, OnCommercialArgs e)
        {
            try
            {
                if (_disposed) return;

                await _statisticsManager.Update(new ChannelCommercial(ChannelName, e.Length));
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing commercial for '{ChannelName}': {err.Message}");
            }
        }

        private async void ClientManager_OnCommunitySubscription(object? sender, OnCommunitySubscriptionArgs e)
        {
            try
            {
                if (_disposed) return;

                var subscription = new ChannelSubscription(SubscriptionType.Community)
                {
                    UserName = e.GiftedSubscription.Login,
                    DisplayName = e.GiftedSubscription.DisplayName,
                    GiftedSubscriptionCount = e.GiftedSubscription.MsgParamMassGiftCount,
                    GiftedSubscriptionPlan = e.GiftedSubscription.MsgParamSubPlan.ToString(),
                    MultiMonth = ParseInt(e.GiftedSubscription.MsgParamMultiMonthGiftDuration, 1)
                };

                await _statisticsManager.Update(subscription);
                await _notificationService.ReceiveSubscriptionAsync(ChannelName, subscription);
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing community subscription for '{ChannelName}': {err.Message}");
            }
        }

        private async void ClientManager_OnUserBanned(object? sender, OnUserBannedArgs e)
        {
            try
            {
                if (_disposed) return;

                var bannedUser = new UserBanned(e.UserBan.Username, e.UserBan.BanReason);

                await _statisticsManager.Update(bannedUser);
                await _notificationService.ReceiveBannedUserAsync(ChannelName, bannedUser);
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing banned user for '{ChannelName}': {err.Message}");
            }
        }

        private async void ClientManager_OnMessageCleared(object? sender, OnMessageClearedArgs e)
        {
            try
            {
                if (_disposed) return;

                var clearedMessage = new ClearedMessage
                {
                    Message = e.Message,
                    TargetMessageId = e.TargetMessageId,
                    TmiSentTs = e.TmiSentTs
                };

                await _statisticsManager.Update(clearedMessage);
                await _notificationService.ReceiveClearedMessageAsync(ChannelName, clearedMessage);
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing cleared message for '{ChannelName}': {err.Message}");
            }
        }

        private async void ClientManager_OnUserTimedOut(object? sender, OnUserTimedoutArgs e)
        {
            try
            {
                if (_disposed) return;

                var timedOutUser = new UserTimedOut
                {
                    Username = e.UserTimeout.Username,
                    TimeoutReason = e.UserTimeout.TimeoutReason,
                    TimeoutDuration = e.UserTimeout.TimeoutDuration
                };

                await _statisticsManager.Update(timedOutUser);
                await _notificationService.ReceiveTimedOutUserAsync(ChannelName, timedOutUser);
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing timed out user for '{ChannelName}': {err.Message}");
            }
        }

        private async void ClientManagerOnChannelStateChanged(object? sender, OnChannelStateChangedArgs e)
        {
            try
            {
                if (_disposed) return;

                await _statisticsManager.Update(e.ChannelState);
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing channel state change for '{ChannelName}': {err.Message}");
            }
        }

        private async void ClientManagerOnOnRaidNotification(object? sender, OnRaidNotificationArgs e)
        {
            try
            {
                if (_disposed) return;

                await _statisticsManager.Update(e.RaidNotification);
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing raid notification for '{ChannelName}': {err.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private static int ParseInt(string? value, int defaultValue)
        {
            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        private static bool IsElevatedUser(ChatMessage message)
        {
            return message.IsModerator || message.IsPartner || message.IsStaff || message.IsVip;
        }

        #endregion
    }
}