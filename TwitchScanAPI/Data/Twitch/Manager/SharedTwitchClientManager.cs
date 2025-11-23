using System.ComponentModel;
using TwitchLib.Client.Enums;

namespace TwitchScanAPI.Data.Twitch.Manager;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Timers;
using Prometheus;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using Timer = System.Timers.Timer;

public class SharedTwitchClientManager : IDisposable
{
    private readonly List<TwitchClientData> _sharedTwitchClients = [];
    private readonly Dictionary<string, ChannelTClientData> _clientassignments = new(StringComparer.OrdinalIgnoreCase);
    
    private const int MaxClients = 5;
    private const int SoftChannelLimit = 15;
    private const int HardChannelLimit = 70;
    private const double RestartIntervalHours = 12.0; // 12 hour restart interval

    private readonly Lock _lock = new();
    private readonly Timer _restartTimer;
    private readonly Timer _inactivityTimer;
    private readonly Timer _metricsUpdateTimer;

    // Prometheus metrics
    private static readonly Gauge ActiveClients = Metrics.CreateGauge("twitch_active_clients", "Number of active Twitch clients");
    private static readonly Gauge ChannelsPerClient = Metrics.CreateGauge("twitch_channels_per_client", "Number of channels per client", "client_id");
    private static readonly Counter ClientRestartsTotal = Metrics.CreateCounter("twitch_client_restarts_total", "Total client restarts");
    private static readonly Counter IrcMessagesTotal = Metrics.CreateCounter("twitch_irc_messages_per_client_total", "Total IRC messages per client", "client_id");
    private static readonly Gauge ClientActivityStatus = Metrics.CreateGauge("twitch_client_activity_status", "Client activity status (1=active, 0=inactive)", "client_id");
    private static readonly Gauge ClientUptimeSeconds = Metrics.CreateGauge("twitch_client_uptime_seconds", "Client uptime in seconds", "client_id");
    private static readonly Gauge ClientMessageRatePerSecond = Metrics.CreateGauge("twitch_client_message_rate_per_second", "Message rate per second for client", "client_id");

    // Struct to hold client and connection time
    private class TwitchClientData(TwitchClient client)
    {
        public string ClientId { get; } = Guid.NewGuid().ToString();
        public TwitchClient Client { get; } = client;
        public DateTime ConnectionTime { get; } = DateTime.UtcNow;
        public DateTime LastActivityTime { get; set; } = DateTime.UtcNow;
        public long MessageCount { get; set; }
        public DateTime LastMessageTime { get; set; } = DateTime.UtcNow;
    }

    private class ChannelTClientData(string channelName)
    {
        public string ChannelName { get; } = channelName;
        public TwitchClient? AssignedClient { get; set; }
    }

    public SharedTwitchClientManager()
    {
        // Initialize timer for client restarts (check every 30 minutes)
        _restartTimer = new Timer(TimeSpan.FromMinutes(30).TotalMilliseconds);
        _restartTimer.Elapsed += CheckAndRestartClients;
        _restartTimer.AutoReset = true;
        _restartTimer.Start();

        // Initialize timer for inactivity checks (check every 10 seconds)
        _inactivityTimer = new Timer(TimeSpan.FromSeconds(10).TotalMilliseconds);
        _inactivityTimer.Elapsed += CheckForInactiveClients;
        _inactivityTimer.AutoReset = true;
        _inactivityTimer.Start();

        // Initialize timer for metrics updates (check every 5 seconds)
        _metricsUpdateTimer = new Timer(TimeSpan.FromSeconds(5).TotalMilliseconds);
        _metricsUpdateTimer.Elapsed += UpdateClientMetrics;
        _metricsUpdateTimer.AutoReset = true;
        _metricsUpdateTimer.Start();

        // Initialize metrics
        UpdateMetrics();
    }

    private (TwitchClient? Client, bool existed) GetOrCreateLeastLoadedClient()
    {
        var underSoftLimit = _sharedTwitchClients
            .Where(c => GetTwitchChatChannelCount(c.Client) < SoftChannelLimit)
            .ToList();
        // Prefer clients under soft limit
        if (underSoftLimit.Any())
        {
            var leastLoaded = underSoftLimit.OrderBy(c => GetTwitchChatChannelCount(c.Client)).First();
            return (leastLoaded.Client, true);
        }

        // If we can add more clients, do so
        if (_sharedTwitchClients.Count < MaxClients)
        {
            var newClient = CreateTwitchClient();
            _sharedTwitchClients.Add(new TwitchClientData(newClient));
            return (newClient, true);
        }

        var underHardLimit = _sharedTwitchClients
            .Where(c => GetTwitchChatChannelCount(c.Client) < HardChannelLimit)
            .ToList();
        // Fallback to client under hard limit
        if (underHardLimit.Any())
        {
            var leastLoaded = underHardLimit.OrderBy(c => GetTwitchChatChannelCount(c.Client)).First();
            return (leastLoaded.Client, true);
        }

        // Cannot add more clients or channels
        Console.WriteLine("All clients at hard limit and max client count reached.");
        return (null, false);
    }
    
    private int GetTwitchChatChannelCount(TwitchClient client)
    {
        return _clientassignments.Count(s => s.Value.AssignedClient == client);
    }
    
    private TwitchClient CreateTwitchClient()
    {
        var credentials = new ConnectionCredentials(GenerateJustinFanUsername(), "");
        var clientOptions = new ClientOptions
        {
            MessagesAllowedInPeriod = 750,
            ThrottlingPeriod = TimeSpan.FromSeconds(30)
        };

        var customClient = new TcpClient(clientOptions);
        var client = new TwitchClient(customClient);
        client.Initialize(credentials, "twitchscanapi");
        client.SendRaw("CAP REQ :twitch.tv/membership");
        client.OnSendReceiveData += (sender, args) =>
        {
            if (args.Direction != SendReceiveDirection.Received) return;
            var clientData = _sharedTwitchClients.FirstOrDefault(c => c.Client == client);
            if (clientData == null) return;
            IrcMessagesTotal.WithLabels(clientData.ClientId).Inc();
        };
        client.OnMessageReceived += (sender, args) =>
        {
            // Update last activity time for the client
            var clientData = _sharedTwitchClients.FirstOrDefault(c => c.Client == client);
            if (clientData != null)
            {
                clientData.LastActivityTime = DateTime.UtcNow;
                clientData.MessageCount++;
                clientData.LastMessageTime = DateTime.UtcNow;
            }

            if (_messageHandlers.TryGetValue(args.ChatMessage.Channel, out var handler))
            {
                handler.Invoke(sender, args);
            }
        };
        
        client.OnMessageCleared += (sender, args) =>
        {
            if (_messageClearedHandlers.TryGetValue(args.Channel, out var handler))
            {
                handler.Invoke(sender, args);
            }
        };

        client.OnUserJoined += (sender, args) =>
        {
            if (_userJoinedHandlers.TryGetValue(args.Channel, out var handler))
            {
                handler.Invoke(sender, args);
            }
            Console.WriteLine($"onjoin test {args.Channel}, {args.Username}");
        };

        client.OnUserLeft += (sender, args) =>
        {
            if (_userLeftHandlers.TryGetValue(args.Channel, out var handler))
            {
                handler.Invoke(sender, args);
            }
            Console.WriteLine($"onleave test {args.Channel}, {args.Username}");
        };
        client.OnNewSubscriber += (sender, args) =>
        {
            if (_newSubHandlers.TryGetValue(args.Channel, out var handler))
            {
                handler.Invoke(sender, args);
            }
        };
        client.OnReSubscriber += (sender, args) =>
        {
            if (_reSubscriberHandlers.TryGetValue(args.Channel, out var handler))
            {
                handler.Invoke(sender, args);
            }
        };
        client.OnGiftedSubscription += (sender, args) =>
        {
            if (_giftedSubscriptionHandlers.TryGetValue(args.Channel, out var handler))
            {
                handler.Invoke(sender, args);
            }
        };
        client.OnCommunitySubscription += (sender, args) =>
        {
            if (_communitySubscriptionHandlers.TryGetValue(args.Channel, out var handler))
            {
                handler.Invoke(sender, args);
            }
        };
        client.OnUserBanned += (sender, args) =>
        {
            if (_userBannedHandlers.TryGetValue(args.UserBan.Channel, out var handler))
            {
                handler.Invoke(sender, args);
            }
        };
        
        client.OnUserTimedout += (sender, args) =>
        {
            if (_userTimeoutHandlers.TryGetValue(args.UserTimeout.Channel, out var handler))
            {
                handler.Invoke(sender, args);
            }
        };
        client.OnChannelStateChanged += (sender, args) =>
        {
            if (_channelStateHandlers.TryGetValue(args.Channel, out var handler))
            {
                handler.Invoke(sender, args);
            }
        };
        client.OnRaidNotification += (sender, args) =>
        {
            if (_raidNotificationHandlers.TryGetValue(args.Channel, out var handler))
            {
                handler.Invoke(sender, args);
            }
        };
        client.OnJoinedChannel += (sender, args) =>
        {
            var channelState = client.GetJoinedChannel(args.Channel).ChannelState;
            var channelStateChanged = new OnChannelStateChangedArgs
            {
                Channel = args.Channel,
                ChannelState = channelState
            };
            if (_channelStateHandlers.TryGetValue(args.Channel, out var handler))
            {
                handler.Invoke(sender, channelStateChanged);
            }
        };
        client.Connect();
        return client;
    }

    public static string GenerateJustinFanUsername()
    {
        var random = new Random();
        var randomNumber = (long)(random.NextDouble() * 1_000_000_000); // up to 9 digits
        return "justinfan" + randomNumber;
    }

    private readonly Dictionary<string, EventHandler<OnMessageReceivedArgs>> _messageHandlers = new();
    private readonly Dictionary<string, EventHandler<OnMessageClearedArgs>> _messageClearedHandlers = new();
    private readonly Dictionary<string, EventHandler<OnUserJoinedArgs>> _userJoinedHandlers = new();
    private readonly Dictionary<string, EventHandler<OnUserLeftArgs>> _userLeftHandlers = new();
    private readonly Dictionary<string, EventHandler<OnNewSubscriberArgs>> _newSubHandlers = new();
    private readonly Dictionary<string, EventHandler<OnReSubscriberArgs>> _reSubscriberHandlers = new();
    private readonly Dictionary<string, EventHandler<OnGiftedSubscriptionArgs>> _giftedSubscriptionHandlers = new();
    private readonly Dictionary<string, EventHandler<OnCommunitySubscriptionArgs>> _communitySubscriptionHandlers = new();
    private readonly Dictionary<string, EventHandler<OnUserBannedArgs>> _userBannedHandlers = new();
    private readonly Dictionary<string, EventHandler<OnUserTimedoutArgs>> _userTimeoutHandlers = new();
    private readonly Dictionary<string, EventHandler<OnChannelStateChangedArgs>> _channelStateHandlers = new();
    private readonly Dictionary<string, EventHandler<OnRaidNotificationArgs>> _raidNotificationHandlers = new();
    
    private void CheckForInactiveClients(object? sender, ElapsedEventArgs e)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            foreach (var clientData in _sharedTwitchClients.ToList())
            {
                // Check if client has been inactive for more than 20 seconds
                if ((now - clientData.LastActivityTime).TotalSeconds >= 60 && clientData.Client.JoinedChannels.Count > 0)
                {
                    RestartInactiveClient(clientData);
                }
            }
        }
    }

    private void RestartInactiveClient(TwitchClientData clientData)
    {
        // Get all channels for this client
        var channelsToRejoin = _clientassignments
            .Where(kvp => kvp.Value.AssignedClient == clientData.Client)
            .Select(kvp => kvp.Key)
            .ToList();

        // Disconnect old client
        clientData.Client.Disconnect();
        ClientActivityStatus.RemoveLabelled(clientData.ClientId);
        ChannelsPerClient.RemoveLabelled(clientData.ClientId);
        ClientUptimeSeconds.RemoveLabelled(clientData.ClientId);
        ClientMessageRatePerSecond.RemoveLabelled(clientData.ClientId);
        IrcMessagesTotal.RemoveLabelled(clientData.ClientId);
        
        _sharedTwitchClients.Remove(clientData);

        // Create new client
        var newClient = CreateTwitchClient();
        _sharedTwitchClients.Add(new TwitchClientData(newClient));

        // Update assignments and rejoin channels
        foreach (var channel in channelsToRejoin)
        {
            try
            {
                newClient.JoinChannel(channel);
                _clientassignments[channel].AssignedClient = newClient;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rejoining channel {channel}: {ex.Message}");
            }
        }

        Console.WriteLine($"Restarted inactive Twitch client. Rejoined {channelsToRejoin.Count} channels.");

        // Update metrics
        ClientRestartsTotal.Inc();
        UpdateMetrics();
    }

    private void CheckAndRestartClients(object? sender, ElapsedEventArgs e)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            foreach (var clientData in _sharedTwitchClients.ToList().Where(clientData => (now - clientData.ConnectionTime).TotalHours >= RestartIntervalHours))
            {
                RestartInactiveClient(clientData);
            }
        }
    }

    private void UpdateMetrics()
    {
        ActiveClients.Set(_sharedTwitchClients.Count);
        foreach (var clientData in _sharedTwitchClients)
        {
            var channelCount = GetTwitchChatChannelCount(clientData.Client);
            ChannelsPerClient.WithLabels(clientData.ClientId).Set(channelCount);
            
            //Debug behavior what channels are connected or now
            Console.WriteLine($"Debug for client {clientData.ClientId}: {channelCount} channels");
            foreach (var keyValuePair in _clientassignments.Where(kvp => kvp.Value.AssignedClient == clientData.Client))
            {
                Console.WriteLine($"Channel:{keyValuePair.Value.ChannelName}");
            }
            Console.WriteLine("----------");
        }
    }

    private void UpdateClientMetrics(object? sender, ElapsedEventArgs e)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            foreach (var clientData in _sharedTwitchClients)
            {
                // Update uptime
                var uptime = (now - clientData.ConnectionTime).TotalSeconds;
                ClientUptimeSeconds.WithLabels(clientData.ClientId).Set(uptime);

                // Update activity status (active if message received in last 20 seconds)
                var isActive = (now - clientData.LastActivityTime).TotalSeconds <= 20 ? 1 : 0;
                ClientActivityStatus.WithLabels(clientData.ClientId).Set(isActive);

                // Update message rate (messages per second over the last minute)
                var timeSinceLastMessage = (now - clientData.LastMessageTime).TotalSeconds;
                var rate = timeSinceLastMessage > 0 ? clientData.MessageCount / timeSinceLastMessage : 0;
                ClientMessageRatePerSecond.WithLabels(clientData.ClientId).Set(rate);
            }
        }
    }

    public void JoinChannel(
        string channelName,
        EventHandler<OnMessageReceivedArgs> onMessageReceived,
        EventHandler<OnUserJoinedArgs> onUserJoined,
        EventHandler<OnUserLeftArgs> onUserLeft,
        EventHandler<OnNewSubscriberArgs> onNewSubscriber,
        EventHandler<OnReSubscriberArgs> onReSubscriber,
        EventHandler<OnGiftedSubscriptionArgs> onGiftedSubscription,
        EventHandler<OnCommunitySubscriptionArgs> onCommunitySubscription,
        EventHandler<OnUserBannedArgs> onUserBanned,
        EventHandler<OnMessageClearedArgs> onMessageCleared,
        EventHandler<OnUserTimedoutArgs> onUserTimedout,
        EventHandler<OnChannelStateChangedArgs> onChannelStateChanged,
        EventHandler<OnRaidNotificationArgs> onRaidNotification
    )
    {
        if (string.IsNullOrWhiteSpace(channelName)) return;
        lock (_lock)
        {
            var client = GetOrCreateLeastLoadedClient();
            var channeldata = new ChannelTClientData(channelName);

            try
            {
                client.Client?.JoinChannel(channelName);
                channeldata.AssignedClient = client.Client;
                _clientassignments[channelName] = channeldata;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while joining channel {channelName}: {ex.Message}");
            }
            _messageHandlers[channelName] = onMessageReceived;
            _userJoinedHandlers[channelName] = onUserJoined;
            _userLeftHandlers[channelName] = onUserLeft;
            _newSubHandlers[channelName] = onNewSubscriber;
            _reSubscriberHandlers[channelName] = onReSubscriber;
            _giftedSubscriptionHandlers[channelName] = onGiftedSubscription;
            _communitySubscriptionHandlers[channelName] = onCommunitySubscription;
            _userBannedHandlers[channelName] = onUserBanned;
            _messageClearedHandlers[channelName] = onMessageCleared;
            _userTimeoutHandlers[channelName] = onUserTimedout;
            _channelStateHandlers[channelName] = onChannelStateChanged;
            _raidNotificationHandlers[channelName] = onRaidNotification;

            // Update metrics after joining channel
            UpdateMetrics();
        }
    }

    public void LeaveChannel(string channelName)
    {
        if (string.IsNullOrWhiteSpace(channelName)) return;
        lock (_lock)
        {
            if (!_clientassignments.TryGetValue(channelName, out var channeldata))
                return;
            var client = channeldata.AssignedClient;
            _clientassignments.Remove(channelName);

            if (client == null) return;
            client.LeaveChannel(channelName);
            if (GetTwitchChatChannelCount(client) == 0 && _sharedTwitchClients.Count > 1)
            {
                client.Disconnect();
                _sharedTwitchClients.RemoveAll(c => c.Client == client);
                Console.WriteLine("Removed unused Twitch Chat Client");
            }

            // Update metrics after leaving channel
            UpdateMetrics();
        }
        _messageHandlers.Remove(channelName);
        _userJoinedHandlers.Remove(channelName);
        _userLeftHandlers.Remove(channelName);
        _newSubHandlers.Remove(channelName);
        _reSubscriberHandlers.Remove(channelName);
        _giftedSubscriptionHandlers.Remove(channelName);
        _communitySubscriptionHandlers.Remove(channelName);
        _userBannedHandlers.Remove(channelName);
        _messageClearedHandlers.Remove(channelName);
        _userTimeoutHandlers.Remove(channelName);
        _channelStateHandlers.Remove(channelName);
        _raidNotificationHandlers.Remove(channelName);
    }

    public void Dispose()
    {
        _restartTimer?.Stop();
        _restartTimer?.Dispose();
        _inactivityTimer?.Stop();
        _inactivityTimer?.Dispose();
        _metricsUpdateTimer?.Stop();
        _metricsUpdateTimer?.Dispose();
        lock (_lock)
        {
            foreach (var clientData in _sharedTwitchClients)
            {
                clientData.Client.Disconnect();
            }
            _sharedTwitchClients.Clear();
            _clientassignments.Clear();
        }
    }
}