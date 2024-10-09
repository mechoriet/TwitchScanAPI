using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchLib.Client.Models;
using TwitchScanAPI.Models.Twitch.Channel;
using TwitchScanAPI.Models.Twitch.Chat;
using TwitchScanAPI.Models.Twitch.User;

namespace TwitchScanAPI.Hubs
{
    public interface ITwitchHub
    {
        Task ReceiveChannelMessage(ChatMessage message);
        Task ReceiveElevatedMessage(ChatMessage message);
        Task ReceiveObservedMessage(ChatMessage message);
        Task ReceiveBannedUser(UserBanned userBanned);
        Task ReceiveTimedOutUser(UserTimedOut userTimedOut);
        Task ReceiveClearedMessage(ClearedMessage clearedMessage);
        Task ReceiveSubscription(ChannelSubscription channelSubscription);
        Task ReceiveUserJoined(string username, string channel);
        Task ReceiveUserLeft(string username);
        // Send all statistics regularly to the client
        Task ReceiveStatistics(IDictionary<string, object> statistics);
        Task ReceiveStatus(ChannelStatus channelStatus);
        Task ReceiveMessageCount(string username, long messageCount);
    }
}