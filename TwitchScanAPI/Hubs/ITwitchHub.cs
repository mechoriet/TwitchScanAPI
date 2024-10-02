using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchScanAPI.Models.Twitch.Channel;
using TwitchScanAPI.Models.Twitch.Chat;
using TwitchScanAPI.Models.Twitch.User;

namespace TwitchScanAPI.Hubs
{
    public interface ITwitchHub
    {
        Task ReceiveChannelMessage(ChannelMessage message);
        Task ReceiveElevatedMessage(ChannelMessage message);
        Task ReceiveObservedMessage(ChannelMessage message);
        Task ReceiveBannedUser(UserBanned userBanned);
        Task ReceiveTimedOutUser(UserTimedOut userTimedOut);
        Task ReceiveClearedMessage(ClearedMessage clearedMessage);
        Task ReceiveSubscription(ChannelSubscription channelSubscription);
        Task ReceiveRaidEvent(ChannelRaid channelRaid);
        Task ReceiveUserJoined(string username, string channel);
        Task ReceiveUserLeft(string username);
        // Send all statistics regularly to the client
        Task ReceiveStatistics(IDictionary<string, object> statistics);
        Task ReceiveOnlineStatus(ChannelStatus channelStatus);
    }
}