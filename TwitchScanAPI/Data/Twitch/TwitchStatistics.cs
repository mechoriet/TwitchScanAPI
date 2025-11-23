using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Prometheus;
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
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        public bool IsOnline { get; private set; }

        // Prometheus metrics
        private static readonly Counter MessagesReceivedTotal = Metrics.CreateCounter("twitch_messages_received_total", "Total messages received", "channel");
        private static readonly Counter MessagesFilteredTotal = Metrics.CreateCounter("twitch_messages_filtered_total", "Total messages filtered", "channel", "reason");
        private static readonly Histogram MessageProcessingDuration = Metrics
            .CreateHistogram(
                name: "twitch_message_processing_duration_seconds",
                help: "Duration of Twitch message processing in seconds",
                labelNames: ["channel"],
                configuration: new HistogramConfiguration
                {
                    // Buckets in seconds, but designed to be read as milliseconds
                    Buckets =
                    [
                        0.001,  // 1 ms
                        0.002,  // 2 ms
                        0.005,  // 5 ms
                        0.010,  // 10 ms
                        0.025,  // 25 ms
                        0.050,  // 50 ms
                        0.075,  // 75 ms
                        0.100,  // 100 ms
                        0.250,  // 250 ms
                        0.500,  // 500 ms
                        0.750,  // 750 ms
                        1.0,    // 1 s
                        2.5,    // 2.5 s
                        5.0,    // 5 s
                        10.0    // 10 s
                    ]
                });

        private static readonly Counter EventsProcessedTotal = Metrics.CreateCounter("twitch_events_processed_total", "Total events processed", "channel", "event_type");

        private static readonly Counter SnapshotsSavedTotal = Metrics.CreateCounter("twitch_snapshots_saved_total", "Total snapshots saved", "channel");
        private static readonly Gauge MessagesInSnapshot = Metrics.CreateGauge("twitch_messages_in_snapshot", "Messages in current snapshot", "channel");
        private static readonly Histogram SnapshotSaveDuration = Metrics.CreateHistogram("twitch_snapshot_save_duration_seconds", "Snapshot save duration", "channel");

        private static readonly Counter ProcessingErrorsTotal = Metrics.CreateCounter("twitch_processing_errors_total", "Total processing errors", "channel", "error_type");

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
                    await SendStatisticsAsync().ConfigureAwait(false);
            };
            _statisticsTimer.Start();
        }

        public string ChannelName { get; }
        public int MessageCount { get; private set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _statisticsManager.Reset();
            _statisticsTimer.Stop();
            _statisticsTimer.Dispose();
            GC.SuppressFinalize(this);
        }

        public static async Task<TwitchStatistics?> CreateAsync(string channelName,
            TwitchManagerFactory clientManagerFactory,
            NotificationService notificationService, MongoDbContext context)
        {
            var clientManager =
                await clientManagerFactory.GetOrCreateClientManagerAsync(channelName).ConfigureAwait(false);

            return clientManager == null
                ? null
                : new TwitchStatistics(channelName, clientManager, notificationService, context);
        }

        public async Task SaveSnapshotAsync(StatisticsManager? manager = null, DateTime? date = null,
            int? viewCount = null)
        {
            if (_disposed) return;

            manager ??= _statisticsManager;

            var stopwatch = Stopwatch.StartNew();

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

                await _context.StatisticHistory.InsertOneAsync(statisticHistory).ConfigureAwait(false);
                Console.WriteLine($"Saved snapshot for channel '{ChannelName}' with {MessageCount} messages");

                // Update metrics
                SnapshotsSavedTotal.WithLabels(ChannelName).Inc();
                MessagesInSnapshot.WithLabels(ChannelName).Set(MessageCount);
                SnapshotSaveDuration.WithLabels(ChannelName).Observe(stopwatch.Elapsed.TotalSeconds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving snapshot for channel '{ChannelName}': {ex.Message} {ex.StackTrace}");
                ProcessingErrorsTotal.WithLabels(ChannelName, "snapshot_save").Inc();
            }
            finally
            {
                stopwatch.Stop();
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
            _clientManager.OnFollowerCountUpdate += ClientManagerFollowerCountUpdate;
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
            // Use a semaphore to prevent multiple simultaneous calls from processing offline status
            await _semaphore.WaitAsync();
            try
            {
                if (_isProcessingOfflineStatus) return;
                _isProcessingOfflineStatus = true;

                // Only save data if we were previously online
                if (IsOnline)
                {
                    IsOnline = false;
                    _statisticsManager.PropagateEvents = false;
                    await SaveSnapshotAsync(date: StartedAt).ConfigureAwait(false);
                    Console.WriteLine($"Channel '{ChannelName}' went offline. Snapshot saved.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling offline status for '{ChannelName}': {ex.Message}");
            }
            finally
            {
                _isProcessingOfflineStatus = false;
                _semaphore.Release();
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
                            _statisticsManager.Reset();
                            break;
                        // If the channel just came online, log it
                        case true when !wasOnline:
                            Console.WriteLine($"Channel '{ChannelName}' is now online.");
                            // Clean any data from statistics manager that might still be in memory
                            _statisticsManager.Reset();
                            StartedAt = DateTime.Now;
                            break;
                    }
                }

                await _notificationService.ReceiveOnlineStatusAsync(new ChannelStatus(ChannelName,
                    channelInformation.IsOnline, MessageCount, channelInformation.Viewers, channelInformation.Uptime)).ConfigureAwait(false);
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
                var channelInfo = await _clientManager.GetChannelInfoAsync().ConfigureAwait(false);

                // If the channel is offline according to the channel info, but we think it's online,
                // update our status
                if (!channelInfo.IsOnline && IsOnline)
                {
                    await HandleChannelOfflineAsync().ConfigureAwait(false);
                }

                await _statisticsManager.Update(channelInfo).ConfigureAwait(false);

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

        private async ValueTask SendStatisticsAsync()
        {
            if (_disposed) return;

            try
            {
                var channelInformation = await _clientManager.GetChannelInfoAsync().ConfigureAwait(false);

                // Double-check if the channel status changed
                if (IsOnline != channelInformation.IsOnline)
                {
                    switch (IsOnline)
                    {
                        case true when !channelInformation.IsOnline:
                            await HandleChannelOfflineAsync().ConfigureAwait(false);
                            break;
                        case false when channelInformation.IsOnline:
                            IsOnline = true;
                            _statisticsManager.PropagateEvents = true;
                            Console.WriteLine($"Channel '{ChannelName}' is now online.");
                            break;
                    }
                }

                await _notificationService.ReceiveOnlineStatusAsync(new ChannelStatus(ChannelName,
                    channelInformation.IsOnline, MessageCount, channelInformation.Viewers, channelInformation.Uptime)).ConfigureAwait(false);

                if (!channelInformation.IsOnline)
                    return;

                var statistics = await GetStatisticsAsync().ConfigureAwait(false);
                await _notificationService.ReceiveStatisticsAsync(ChannelName, statistics).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SendStatisticsAsync for '{ChannelName}': {ex.Message}");
            }
        }

        #region Event Handlers

        private async void ClientManager_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (_disposed) return;

                var chatMessage = e.ChatMessage;
                var channelMessage = new ChannelMessage(ChannelName, new TwitchChatMessage
                {
                    Username = chatMessage.Username,
                    Message = chatMessage.Message,
                    ColorHex = string.IsInterned(chatMessage.ColorHex) ?? string.Intern(chatMessage.ColorHex),
                    Bits = chatMessage.Bits,
                    BitsInDollars = chatMessage.BitsInDollars,
                    FirstTime = chatMessage.IsFirstMessage,
                    Emotes = e.ChatMessage.EmoteSet.Emotes
                        .Select(em => new TwitchEmote(em.Id, em.Name, em.StartIndex, em.EndIndex))
                        .ToList()
                });

                // Add BTTV and 7TV emotes to the message
                StaticTwitchHelper.AddEmotesToMessage(channelMessage, _clientManager.ExternalChannelEmotes);
                await _notificationService.ReceiveChannelMessageAsync(ChannelName, channelMessage).ConfigureAwait(false);
                await _notificationService.ReceiveMessageCountAsync(ChannelName, MessageCount).ConfigureAwait(false);

                // Update message count and statistics if not a bot
                if (!Variables.BotNames.Contains(channelMessage.ChatMessage.Username, StringComparer.OrdinalIgnoreCase))
                {
                    MessageCount++;
                    MessagesReceivedTotal.WithLabels(ChannelName).Inc();
                    await _statisticsManager.Update(channelMessage).ConfigureAwait(false);
                }
                else
                {
                    MessagesFilteredTotal.WithLabels(ChannelName, "bot").Inc();
                }

                // Check for observed words
                if (_observedWordsManager.IsMatch(channelMessage.ChatMessage.Message))
                    await _notificationService.ReceiveObservedMessageAsync(ChannelName, channelMessage).ConfigureAwait(false);

                // Check for elevated users
                if (IsElevatedUser(chatMessage))
                    await _notificationService.ReceiveElevatedMessageAsync(ChannelName, channelMessage).ConfigureAwait(false);

                MessageProcessingDuration.WithLabels(ChannelName).Observe(stopwatch.Elapsed.TotalSeconds);
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing message for '{ChannelName}': {err.Message}");
                ProcessingErrorsTotal.WithLabels(ChannelName, "message_processing").Inc();
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        private async void ClientManager_OnUserJoined(object? sender, OnUserJoinedArgs e)
        {
            try
            {
                if (_disposed) return;

                if (!_userManager.AddUser(e.Username)) return;
                await _statisticsManager.Update(new UserJoined(e.Username)).ConfigureAwait(false);
                await _notificationService.ReceiveUserJoinedAsync(ChannelName, e.Username, e.Channel).ConfigureAwait(false);
                EventsProcessedTotal.WithLabels(ChannelName, "user_joined").Inc();
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing user join for '{ChannelName}': {err.Message}");
                ProcessingErrorsTotal.WithLabels(ChannelName, "user_join").Inc();
            }
        }

        private async void ClientManager_OnUserLeft(object? sender, OnUserLeftArgs e)
        {
            try
            {
                if (_disposed) return;

                if (!_userManager.RemoveUser(e.Username)) return;
                await _statisticsManager.Update(new UserLeft(e.Username)).ConfigureAwait(false);
                await _notificationService.ReceiveUserLeftAsync(ChannelName, e.Username).ConfigureAwait(false);
                EventsProcessedTotal.WithLabels(ChannelName, "user_left").Inc();
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing user leave for '{ChannelName}': {err.Message}");
                ProcessingErrorsTotal.WithLabels(ChannelName, "user_leave").Inc();
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
                    SubscriptionPlanObj = e.Subscriber.SubscriptionPlan,
                    Months = 1,
                    MultiMonth = ParseInt(e.Subscriber.MsgParamCumulativeMonths, 1)
                };

                await _statisticsManager.Update(subscription).ConfigureAwait(false);
                await _notificationService.ReceiveSubscriptionAsync(ChannelName, subscription).ConfigureAwait(false);
                EventsProcessedTotal.WithLabels(ChannelName, "new_subscriber").Inc();
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing new subscriber for '{ChannelName}': {err.Message}");
                ProcessingErrorsTotal.WithLabels(ChannelName, "new_subscriber").Inc();
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
                    SubscriptionPlanObj = e.ReSubscriber.SubscriptionPlan,
                    Months = ParseInt(e.ReSubscriber.MsgParamStreakMonths, 1),
                    MultiMonth = ParseInt(e.ReSubscriber.MsgParamCumulativeMonths, 1)
                };

                await _statisticsManager.Update(subscription).ConfigureAwait(false);
                await _notificationService.ReceiveSubscriptionAsync(ChannelName, subscription).ConfigureAwait(false);
                EventsProcessedTotal.WithLabels(ChannelName, "re_subscriber").Inc();
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing re-subscriber for '{ChannelName}': {err.Message}");
                ProcessingErrorsTotal.WithLabels(ChannelName, "re_subscriber").Inc();
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
                    SubscriptionPlanObj = e.GiftedSubscription.MsgParamSubPlan,
                    Months = ParseInt(e.GiftedSubscription.MsgParamMultiMonthGiftDuration, 1),
                    MultiMonth = ParseInt(e.GiftedSubscription.MsgParamMonths, 1),
                    Message = e.GiftedSubscription.SystemMsg,
                    GiftedSubscriptionPlan = e.GiftedSubscription.MsgParamSubPlanName
                };

                await _statisticsManager.Update(subscription).ConfigureAwait(false);
                await _notificationService.ReceiveSubscriptionAsync(ChannelName, subscription).ConfigureAwait(false);
                EventsProcessedTotal.WithLabels(ChannelName, "gifted_subscription").Inc();
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing gifted subscription for '{ChannelName}': {err.Message}");
                ProcessingErrorsTotal.WithLabels(ChannelName, "gifted_subscription").Inc();
            }
        }

        private async void ClientManager_OnCommercial(object? sender, OnCommercialArgs e)
        {
            try
            {
                if (_disposed) return;
                await _statisticsManager.Update(new ChannelCommercial(ChannelName, e.Length)).ConfigureAwait(false);
                EventsProcessedTotal.WithLabels(ChannelName, "commercial").Inc();
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing commercial for '{ChannelName}': {err.Message}");
                ProcessingErrorsTotal.WithLabels(ChannelName, "commercial").Inc();
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
                    SubscriptionPlanObj = e.GiftedSubscription.MsgParamSubPlan,
                    MultiMonth = ParseInt(e.GiftedSubscription.MsgParamMultiMonthGiftDuration, 1)
                };

                await _statisticsManager.Update(subscription).ConfigureAwait(false);
                await _notificationService.ReceiveSubscriptionAsync(ChannelName, subscription).ConfigureAwait(false);
                EventsProcessedTotal.WithLabels(ChannelName, "community_subscription").Inc();
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing community subscription for '{ChannelName}': {err.Message}");
                ProcessingErrorsTotal.WithLabels(ChannelName, "community_subscription").Inc();
            }
        }

        private async void ClientManager_OnUserBanned(object? sender, OnUserBannedArgs e)
        {
            try
            {
                if (_disposed) return;

                var bannedUser = new UserBanned(e.UserBan.Username, e.UserBan.BanReason);

                await _statisticsManager.Update(bannedUser).ConfigureAwait(false);
                await _notificationService.ReceiveBannedUserAsync(ChannelName, bannedUser).ConfigureAwait(false);
                EventsProcessedTotal.WithLabels(ChannelName, "user_banned").Inc();
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing banned user for '{ChannelName}': {err.Message}");
                ProcessingErrorsTotal.WithLabels(ChannelName, "user_banned").Inc();
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

                await _statisticsManager.Update(clearedMessage).ConfigureAwait(false);
                await _notificationService.ReceiveClearedMessageAsync(ChannelName, clearedMessage).ConfigureAwait(false);
                EventsProcessedTotal.WithLabels(ChannelName, "message_cleared").Inc();
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing cleared message for '{ChannelName}': {err.Message}");
                ProcessingErrorsTotal.WithLabels(ChannelName, "message_cleared").Inc();
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

                await _statisticsManager.Update(timedOutUser).ConfigureAwait(false);
                await _notificationService.ReceiveTimedOutUserAsync(ChannelName, timedOutUser).ConfigureAwait(false);
                EventsProcessedTotal.WithLabels(ChannelName, "user_timed_out").Inc();
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing timed out user for '{ChannelName}': {err.Message}");
                ProcessingErrorsTotal.WithLabels(ChannelName, "user_timed_out").Inc();
            }
        }

        private async void ClientManagerOnChannelStateChanged(object? sender, OnChannelStateChangedArgs e)
        {
            try
            {
                if (_disposed) return;

                await _statisticsManager.Update(e.ChannelState).ConfigureAwait(false);
                EventsProcessedTotal.WithLabels(ChannelName, "channel_state_changed").Inc();
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing channel state change for '{ChannelName}': {err.Message}");
                ProcessingErrorsTotal.WithLabels(ChannelName, "channel_state_changed").Inc();
            }
        }

        private async void ClientManagerOnOnRaidNotification(object? sender, OnRaidNotificationArgs e)
        {
            try
            {
                if (_disposed) return;

                await _statisticsManager.Update(e.RaidNotification).ConfigureAwait(false);
                EventsProcessedTotal.WithLabels(ChannelName, "raid_notification").Inc();
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing raid notification for '{ChannelName}': {err.Message}");
                ProcessingErrorsTotal.WithLabels(ChannelName, "raid_notification").Inc();
            }
        }
        
        private async void ClientManagerFollowerCountUpdate(object? sender, ChannelFollowers e)
        {
            try
            {
                if (_disposed) return;
                await _statisticsManager.Update(e).ConfigureAwait(false);
                EventsProcessedTotal.WithLabels(ChannelName, "follower_count_update").Inc();
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error processing followers count for '{ChannelName}': {err.Message}");
                ProcessingErrorsTotal.WithLabels(ChannelName, "follower_count_update").Inc();
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