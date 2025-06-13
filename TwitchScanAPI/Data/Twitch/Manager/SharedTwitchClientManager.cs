using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Configuration;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace TwitchScanAPI.Data.Twitch.Manager
{
    public class SharedTwitchClientManager : IDisposable
    {

        private readonly List<TwitchClient> _sharedTwitchClients = [];
        private readonly Dictionary<string, ChannelTClientData> _clientassignments = new(StringComparer.OrdinalIgnoreCase);

        private const int MaxClients = 4;
        private const int SoftChannelLimit = 20;
        private const int HardChannelLimit = 80;

        private readonly Lock _lock = new();

        private (TwitchClient? Client, bool existed) GetOrCreateLeastLoadedClient()
        {
            var underSoftLimit = _sharedTwitchClients
                .Where(c => GetTwitchChatChannelCount(c) < SoftChannelLimit)
                .ToList();

            var underHardLimit = _sharedTwitchClients
                .Where(c => GetTwitchChatChannelCount(c) < HardChannelLimit)
                .ToList();

            /*Console.WriteLine(_sharedTwitchClients.Count + " clients loaded");
            foreach (var client in _sharedTwitchClients)
            {
                Console.WriteLine($"client: {client.ConnectionCredentials.TwitchUsername} | channels: {client.JoinedChannels.Count}");
            }*/

            // Prefer clients under soft limit
            if (underSoftLimit.Any())
            {
                var leastLoaded = underSoftLimit.OrderBy(GetTwitchChatChannelCount).First();
                return (leastLoaded, true);
            }

            // If we can add more clients, do so
            if (_sharedTwitchClients.Count < MaxClients)
            {
                var newClient = CreateTwitchClient();
                _sharedTwitchClients.Add(newClient);
                return (newClient, true);
            }

            // Fallback to client under hard limit
            if (underHardLimit.Any())
            {
                var leastLoaded = underHardLimit.OrderBy(GetTwitchChatChannelCount).First();
                return (leastLoaded, true);
            }

            // Cannot add more clients or channels
            Console.WriteLine("All clients at hard limit and max client count reached.");
            return (null, false);
        }
        
        private int GetTwitchChatChannelCount(TwitchClient client)
        {
            return _clientassignments.Count(s => s.Value.AssignedClient == client);
        }
        
        private class ChannelTClientData(string channelName)
        {
            public string ChannelName { get; } = channelName;
            public TwitchClient? AssignedClient { get; set; }
        }

        private TwitchClient CreateTwitchClient()
        {
            var credentials = new ConnectionCredentials(GenerateJustinFanUsername(), "");
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };

            var customClient = new WebSocketClient(clientOptions);
            var client = new TwitchClient(customClient);
            client.Initialize(credentials, "twitchscanapi");
            client.OnMessageReceived += (sender, args) =>
            {
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
            };

            client.OnUserLeft += (sender, args) =>
            {
                if (_userLeftHandlers.TryGetValue(args.Channel, out var handler))
                {
                    handler.Invoke(sender, args);
                }
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
                if (_giftedSubscribtionHandlers.TryGetValue(args.Channel, out var handler))
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
        private readonly Dictionary<string, EventHandler<OnGiftedSubscriptionArgs>> _giftedSubscribtionHandlers = new();
        private readonly Dictionary<string, EventHandler<OnCommunitySubscriptionArgs>> _communitySubscriptionHandlers = new();
        private readonly Dictionary<string, EventHandler<OnUserBannedArgs>> _userBannedHandlers = new();
        private readonly Dictionary<string, EventHandler<OnUserTimedoutArgs>> _userTimeoutHandlers = new();
        private readonly Dictionary<string, EventHandler<OnChannelStateChangedArgs>> _channelStateHandlers = new();
        private readonly Dictionary<string,EventHandler<OnRaidNotificationArgs> > _raidNotificationHandlers = new();
        //private Timer _reloadTokenTimer;
        
        private void UpdateToken(IConfiguration configuration)
        {
            // Update the token in the TwitchClient
            //_client.SetConnectionCredentials(new ConnectionCredentials(configuration.GetValue<string>(Variables.TwitchChatName),configuration.GetValue<string>(Variables.TwitchOauthKey)));
            Console.WriteLine("Twitch client token updated.");
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
            _giftedSubscribtionHandlers[channelName] = onGiftedSubscription;
            _communitySubscriptionHandlers[channelName] = onCommunitySubscription;
            _userBannedHandlers[channelName] = onUserBanned;
            _messageClearedHandlers[channelName] = onMessageCleared;
            _userTimeoutHandlers[channelName] = onUserTimedout;
            _channelStateHandlers[channelName] = onChannelStateChanged;
            _raidNotificationHandlers[channelName] = onRaidNotification;
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
                if (GetTwitchChatChannelCount(client) != 0 || _sharedTwitchClients.Count <= 1) return;
                
                client.Disconnect();
                _sharedTwitchClients.Remove(client);
                Console.WriteLine("Removed unused Twitch Chat Client");
            }
            //_client.LeaveChannel(channelName);
            _messageHandlers.Remove(channelName);
            _userJoinedHandlers.Remove(channelName);
            _userLeftHandlers.Remove(channelName);
            _newSubHandlers.Remove(channelName);
            _reSubscriberHandlers.Remove(channelName);
            _giftedSubscribtionHandlers.Remove(channelName);
            _communitySubscriptionHandlers.Remove(channelName);
            _userBannedHandlers.Remove(channelName);
            _messageClearedHandlers.Remove(channelName);
            _userTimeoutHandlers.Remove(channelName);
            _channelStateHandlers.Remove(channelName);
            _raidNotificationHandlers.Remove(channelName);
        }

        public void Dispose()
        {
            _sharedTwitchClients.Clear();
            _clientassignments.Clear();
        }
    }
}
