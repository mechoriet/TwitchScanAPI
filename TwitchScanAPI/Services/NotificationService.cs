// NotificationService.cs

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using TwitchScanAPI.Hubs;
using TwitchScanAPI.Models.Twitch.Channel;
using TwitchScanAPI.Models.Twitch.Chat;
using TwitchScanAPI.Models.Twitch.User;

namespace TwitchScanAPI.Services
{
    public class NotificationService
    {
        private readonly IHubContext<TwitchHub, ITwitchHub> _hubContext;

        public NotificationService(IHubContext<TwitchHub, ITwitchHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task ReceiveStatisticsAsync(string channelName, IDictionary<string, object> statistics)
        {
            return _hubContext.Clients.Group(channelName).ReceiveStatistics(statistics);
        }

        public Task ReceiveChannelMessageAsync(string channelName, ChannelMessage message)
        {
            return _hubContext.Clients.Group(channelName).ReceiveChannelMessage(message);
        }
        
        public Task ReceiveMessageCountAsync(string channelName, long messageCount)
        {
            return _hubContext.Clients.All.ReceiveMessageCount(channelName, messageCount);
        }

        public Task ReceiveObservedMessageAsync(string channelName, ChannelMessage message)
        {
            return _hubContext.Clients.Group(channelName).ReceiveObservedMessage(message);
        }

        public Task ReceiveElevatedMessageAsync(string channelName, ChannelMessage message)
        {
            return _hubContext.Clients.Group(channelName).ReceiveElevatedMessage(message);
        }

        public Task ReceiveUserJoinedAsync(string channelName, string username, string channel)
        {
            return _hubContext.Clients.Group(channelName).ReceiveUserJoined(username, channel);
        }

        public Task ReceiveUserLeftAsync(string channelName, string username)
        {
            return _hubContext.Clients.Group(channelName).ReceiveUserLeft(username);
        }

        public Task ReceiveSubscriptionAsync(string channelName, ChannelSubscription subscription)
        {
            return _hubContext.Clients.Group(channelName).ReceiveSubscription(subscription);
        }

        public Task ReceiveBannedUserAsync(string channelName, UserBanned bannedUser)
        {
            return _hubContext.Clients.Group(channelName).ReceiveBannedUser(bannedUser);
        }

        public Task ReceiveClearedMessageAsync(string channelName, ClearedMessage clearedMessage)
        {
            return _hubContext.Clients.Group(channelName).ReceiveClearedMessage(clearedMessage);
        }

        public Task ReceiveTimedOutUserAsync(string channelName, UserTimedOut timedOutUser)
        {
            return _hubContext.Clients.Group(channelName).ReceiveTimedOutUser(timedOutUser);
        }
        
        public Task ReceiveOnlineStatusAsync(ChannelStatus channelStatus)
        {
            return _hubContext.Clients.All.ReceiveStatus(channelStatus);
        }
    }
}
