using System.Threading.Tasks;
using TwitchScanAPI.Models;
using TwitchScanAPI.Models.Twitch;
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
        Task ReceiveHostEvent(ChannelHost channelHost);
        Task ReceiveUserJoined(string username, string channel);
        Task ReceiveUserLeft(string username);
    }

}