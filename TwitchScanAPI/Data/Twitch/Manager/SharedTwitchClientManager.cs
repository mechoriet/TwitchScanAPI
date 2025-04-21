using System;
using System.Collections.Generic;
using System.Timers;
using Microsoft.Extensions.Configuration;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchScanAPI.Global;

namespace TwitchScanAPI.Data.Twitch.Manager
{
    public class SharedTwitchClientManager : IDisposable
    {
        private readonly TwitchClient _client;
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
        private Timer _reloadTokenTimer;

        public SharedTwitchClientManager(IConfiguration configuration)
        {
            _reloadTokenTimer = new Timer();
            _reloadTokenTimer.Interval = 1000 * 60 * 30; // 30 minutes
            _reloadTokenTimer.Elapsed += (sender, args) =>
            {
                UpdateToken(configuration);
            };
            this._reloadTokenTimer.Start();
            // Initialize the Twitch client
            // Set up the connection credentials
            //var credentials = new ConnectionCredentials(configuration.GetValue<string>(Variables.TwitchChatName),configuration.GetValue<string>(Variables.TwitchOauthKey));
            
            var credentials = new ConnectionCredentials("justinfan137193718917", "");
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };

            var customClient = new WebSocketClient(clientOptions);
            _client = new TwitchClient(customClient);
            _client.Initialize(credentials, "twitchscanapi");

            _client.OnMessageReceived += (sender, args) =>
            {
                if (_messageHandlers.TryGetValue(args.ChatMessage.Channel, out var handler))
                {
                    handler.Invoke(sender, args);
                }
            };
            
            _client.OnMessageCleared += (sender, args) =>
            {
                if (_messageClearedHandlers.TryGetValue(args.Channel, out var handler))
                {
                    handler.Invoke(sender, args);
                }
            };

            _client.OnUserJoined += (sender, args) =>
            {
                if (_userJoinedHandlers.TryGetValue(args.Channel, out var handler))
                {
                    handler.Invoke(sender, args);
                }
            };

            _client.OnUserLeft += (sender, args) =>
            {
                if (_userLeftHandlers.TryGetValue(args.Channel, out var handler))
                {
                    handler.Invoke(sender, args);
                }
            };
            _client.OnNewSubscriber += (sender, args) =>
            {
                if (_newSubHandlers.TryGetValue(args.Channel, out var handler))
                {
                    handler.Invoke(sender, args);
                }
            };
            _client.OnReSubscriber += (sender, args) =>
            {
                if (_reSubscriberHandlers.TryGetValue(args.Channel, out var handler))
                {
                    handler.Invoke(sender, args);
                }
            };
            _client.OnGiftedSubscription += (sender, args) =>
            {
                if (_giftedSubscribtionHandlers.TryGetValue(args.Channel, out var handler))
                {
                    handler.Invoke(sender, args);
                }
            };
            _client.OnCommunitySubscription += (sender, args) =>
            {
                if (_communitySubscriptionHandlers.TryGetValue(args.Channel, out var handler))
                {
                    handler.Invoke(sender, args);
                }
            };
            _client.OnUserBanned += (sender, args) =>
            {
                if (_userBannedHandlers.TryGetValue(args.UserBan.Channel, out var handler))
                {
                    handler.Invoke(sender, args);
                }
            };
            
            _client.OnUserTimedout += (sender, args) =>
            {
                if (_userTimeoutHandlers.TryGetValue(args.UserTimeout.Channel, out var handler))
                {
                    handler.Invoke(sender, args);
                }
            };
            _client.OnChannelStateChanged += (sender, args) =>
            {
                if (_channelStateHandlers.TryGetValue(args.Channel, out var handler))
                {
                    handler.Invoke(sender, args);
                }
            };
            _client.OnRaidNotification += (sender, args) =>
            {
                if (_raidNotificationHandlers.TryGetValue(args.Channel, out var handler))
                {
                    handler.Invoke(sender, args);
                }
            };
            _client.OnLog += (sender, args) =>
            {
                var logMessage = args.Data;
                //Console.WriteLine($"[LOG] {logMessage}");
                if (logMessage.Contains("PRIVMSG") || logMessage.Contains("JOIN") || logMessage.Contains("PART") || logMessage.Contains("USERNOTICE") || logMessage.Contains("ROOMSTATE") || args.Data.Contains("CLEARCHAT") || logMessage.Contains("PONG") || logMessage.Contains("PING"))
                {
                    // Filter out unwanted log messages
                    return;
                }
                Console.WriteLine($"[LOG] shared {logMessage}");
            };
            
            _client.Connect();
        }
        
        private void UpdateToken(IConfiguration configuration)
        {
            // Update the token in the TwitchClient
            _client.SetConnectionCredentials(new ConnectionCredentials(configuration.GetValue<string>(Variables.TwitchChatName),configuration.GetValue<string>(Variables.TwitchOauthKey)));
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
            _client.JoinChannel(channelName);
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

        public void LeaveChannel(string channelName)
        {
            _client.LeaveChannel(channelName);
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
            _client.Disconnect();
        }
    }
}
