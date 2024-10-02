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
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
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
        // Twitch Clients and API
        private TwitchClient? _client;
        private readonly TwitchAPI _api = new();
        private readonly IHubContext<TwitchHub, ITwitchHub> _hubContext;
        private readonly IConfiguration _configuration;

        // Channel Information
        public string ChannelName { get; }
        public int MessageCount { get; private set; }
        public DateTime StartedAt { get; } = DateTime.Now;
        public ConcurrentDictionary<string, string> Users { get; } = new(StringComparer.OrdinalIgnoreCase);

        // Statistics
        private readonly Statistics.Base.Statistics _statistics = new();

        // Words to Observe
        private readonly HashSet<string> _wordsToObserve = new(StringComparer.OrdinalIgnoreCase);
        private Regex? _observePatternRegex;

        // Timers
        private readonly Timer _statisticsTimer;
        private readonly Timer _reconnectTimer;
        private readonly TimeSpan _sendInterval = TimeSpan.FromSeconds(60);
        private readonly TimeSpan _retryInterval = TimeSpan.FromSeconds(30);
        private bool _isReconnecting;

        // Configuration Keys
        private const string ClientIdKey = Variables.TwitchClientId;
        private const string ClientSecretKey = Variables.TwitchClientSecret;
        private const string OauthKey = Variables.TwitchOauthKey;
        private const string ChatNameKey = Variables.TwitchChatName;

        public TwitchStatistics(string channelName, IHubContext<TwitchHub, ITwitchHub> hubContext,
            IConfiguration configuration)
        {
            ChannelName = channelName;
            _hubContext = hubContext;
            _configuration = configuration;

            _statisticsTimer = new Timer(_sendInterval.TotalMilliseconds)
            {
                AutoReset = true
            };
            _statisticsTimer.Elapsed += async (_, _) => await SendStatisticsAsync();
            
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

        /// <summary>
        /// Adds a word to the observation list and updates the regex pattern.
        /// </summary>
        /// <param name="text">The word to observe.</param>
        public void AddTextToObserve(string text)
        {
            if (_wordsToObserve.Add(text))
            {
                UpdateRegex();
            }
        }

        /// <summary>
        /// Initializes the Twitch client and starts monitoring.
        /// </summary>
        public async Task InitializeClientAsync()
        {
            ConfigureTwitchApi();

            await AttemptConnectionAsync();

            _statisticsTimer.Start();
        }

        /// <summary>
        /// Configures the Twitch API with necessary credentials.
        /// </summary>
        private void ConfigureTwitchApi()
        {
            var clientId = _configuration.GetValue<string>(ClientIdKey);
            var clientSecret = _configuration.GetValue<string>(ClientSecretKey);

            _api.Settings.ClientId = clientId;
            _api.Settings.Secret = clientSecret;
        }

        /// <summary>
        /// Attempts to connect to the Twitch channel. If offline, schedules a retry.
        /// </summary>
        public async Task AttemptConnectionAsync()
        {
            if (await IsChannelOnlineAsync())
            {
                await StartClientAsync();
            }
            else
            {
                Console.WriteLine($"Channel '{ChannelName}' is offline. Scheduling reconnect...");
                ScheduleReconnect();
            }
        }

        /// <summary>
        /// Schedules a reconnect attempt after a predefined interval.
        /// </summary>
        private void ScheduleReconnect()
        {
            if (_isReconnecting)
                return;

            _isReconnecting = true;
            _reconnectTimer.Start();
        }

        /// <summary>
        /// Starts the Twitch client and sets up event handlers.
        /// </summary>
        private Task StartClientAsync()
        {
            if (_client is { IsConnected: true })
                return Task.CompletedTask;

            var credentials = new ConnectionCredentials(
                _configuration.GetValue<string>(ChatNameKey),
                _configuration.GetValue<string>(OauthKey));

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
            _client.Initialize(credentials, ChannelName);

            SubscribeToClientEvents();

            _client.Connect();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Subscribes to Twitch client events.
        /// </summary>
        private void SubscribeToClientEvents()
        {
            if (_client == null) return;

            // Chat Events
            _client.OnMessageReceived += Client_OnMessageReceived;
            _client.OnNewSubscriber += Client_OnNewSubscriber;
            _client.OnReSubscriber += Client_OnReSubscriber;
            _client.OnGiftedSubscription += Client_OnGiftedSubscription;
            _client.OnCommunitySubscription += Client_OnCommunitySubscription;
            _client.OnUserTimedout += Client_OnUserTimedOut;
            _client.OnMessageCleared += Client_OnMessageCleared;
            _client.OnUserBanned += Client_OnUserBanned;
            _client.OnUserJoined += Client_OnUserJoined;
            _client.OnUserLeft += Client_OnUserLeft;
            _client.OnRaidNotification += Client_OnRaid;
        }

        /// <summary>
        /// Configures the observation regex based on the words to observe.
        /// </summary>
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

        /// <summary>
        /// Checks if the Twitch channel is currently online.
        /// </summary>
        /// <returns>True if online; otherwise, false.</returns>
        private async Task<bool> IsChannelOnlineAsync()
        {
            try
            {
                var streams = await _api.Helix.Streams.GetStreamsAsync(userLogins: new List<string> { ChannelName });
                return streams?.Streams.Any() ?? false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking channel status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disconnects the Twitch client gracefully.
        /// </summary>
        private void DisconnectClient()
        {
            if (_client?.IsConnected != true) return;
            _client.Disconnect();
            _client = null;
        }

        /// <summary>
        /// Periodically sends statistics to connected clients.
        /// </summary>
        private async Task SendStatisticsAsync()
        {
            if (!await IsChannelOnlineAsync())
            {
                Console.WriteLine($"Channel '{ChannelName}' went offline. Disconnecting client.");
                DisconnectClient();
                ScheduleReconnect();
                return;
            }

            if (_client is not { IsConnected: true })
            {
                await AttemptConnectionAsync();
                return;
            }

            var statistics = await GetStatisticsAsync();
            await _hubContext.Clients.Group(ChannelName).ReceiveStatistics(statistics);
        }

        /// <summary>
        /// Retrieves the latest statistics from the Twitch API.
        /// </summary>
        /// <returns>A dictionary containing statistics data.</returns>
        public async Task<IDictionary<string, object>> GetStatisticsAsync()
        {
            try
            {
                var streams = await _api.Helix.Streams.GetStreamsAsync(userLogins: new List<string> { ChannelName });
                var stream = streams?.Streams.FirstOrDefault();

                var channelInfo = new ChannelInformation
                {
                    Viewers = stream?.ViewerCount ?? 0,
                    Title = stream?.Title ?? "No Title",
                    Game = stream?.GameName ?? "No Game",
                    Uptime = stream?.StartedAt ?? DateTime.MinValue,
                    Thumbnail = stream?.ThumbnailUrl ?? "No Thumbnail",
                    StreamType = stream?.Type ?? "Offline"
                };

                _statistics.Update(channelInfo);

                return _statistics.GetAllStatistics() ?? new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching statistics: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }

        #region Event Handlers

        private async void Client_OnUserJoined(object? sender, OnUserJoinedArgs e)
        {
            if (!Users.TryAdd(e.Username, e.Channel)) return;
            _statistics.Update(new UserJoined(e.Username));
            await _hubContext.Clients.Group(ChannelName).ReceiveUserJoined(e.Username, e.Channel);
        }

        private async void Client_OnUserLeft(object? sender, OnUserLeftArgs e)
        {
            if (Users.TryRemove(e.Username, out _))
            {
                _statistics.Update(new UserLeft(e.Username));
                await _hubContext.Clients.Group(ChannelName).ReceiveUserLeft(e.Username);
            }
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
                MultiMonth = ParseInt(e.Subscriber.MsgParamCumulativeMonths, 1),
            };

            _statistics.Update(subscription);
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
                Months = ParseInt(e.ReSubscriber.MsgParamStreakMonths, 1),
                MultiMonth = ParseInt(e.ReSubscriber.MsgParamCumulativeMonths, 1)
            };

            _statistics.Update(subscription);
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
                Months = ParseInt(e.GiftedSubscription.MsgParamMonths, 1),
                MultiMonth = ParseInt(e.GiftedSubscription.MsgParamMultiMonthGiftDuration, 1),
                Message = e.GiftedSubscription.SystemMsg,
                GiftedSubscriptionPlan = e.GiftedSubscription.MsgParamSubPlanName
            };

            _statistics.Update(subscription);
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
                MultiMonth = ParseInt(e.GiftedSubscription.MsgParamMultiMonthGiftDuration, 1),
            };

            _statistics.Update(subscription);
            await _hubContext.Clients.Group(ChannelName).ReceiveSubscription(subscription);
        }

        private async void Client_OnRaid(object? sender, OnRaidNotificationArgs e)
        {
            var raidEvent = new ChannelRaid
            {
                Raider = e.RaidNotification.MsgParamDisplayName,
                ViewerCount = ParseInt(e.RaidNotification.MsgParamViewerCount, 0)
            };

            _statistics.Update(raidEvent);
            await _hubContext.Clients.Group(ChannelName).ReceiveRaidEvent(raidEvent);
        }

        private async void Client_OnUserBanned(object? sender, OnUserBannedArgs e)
        {
            var bannedUser = new UserBanned(e.UserBan.Username, e.UserBan.BanReason);

            _statistics.Update(bannedUser);
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

            _statistics.Update(clearedMessage);
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

            _statistics.Update(timedOutUser);
            await _hubContext.Clients.Group(ChannelName).ReceiveTimedOutUser(timedOutUser);
        }

        private async void Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
        {
            var chatMessage = e.ChatMessage;
            var channelMessage = new ChannelMessage(ChannelName, chatMessage);
            await _hubContext.Clients.Group(ChannelName).ReceiveChannelMessage(channelMessage);

            // Update message count and statistics if not a bot
            if (!Variables.BotNames.Contains(chatMessage.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                MessageCount++;
                _statistics.Update(channelMessage);
            }

            // Check for observed words
            if (_observePatternRegex?.IsMatch(chatMessage.Message) == true)
            {
                await _hubContext.Clients.Group(ChannelName).ReceiveObservedMessage(channelMessage);
            }

            // Check for elevated users
            if (IsElevatedUser(chatMessage))
            {
                await _hubContext.Clients.Group(ChannelName).ReceiveElevatedMessage(channelMessage);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Parses an integer from a string with a default value.
        /// </summary>
        private int ParseInt(string? value, int defaultValue) =>
            int.TryParse(value, out var result) ? result : defaultValue;

        /// <summary>
        /// Determines if a user has elevated privileges.
        /// </summary>
        private bool IsElevatedUser(ChatMessage message) =>
            message.IsModerator || message.IsPartner || message.IsStaff || message.IsVip;

        #endregion

        /// <summary>
        /// Disposes resources gracefully.
        /// </summary>
        public void Dispose()
        {
            DisconnectClient();
            _statisticsTimer.Stop();
            _statisticsTimer.Dispose();
            _reconnectTimer.Stop();
            _reconnectTimer.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
