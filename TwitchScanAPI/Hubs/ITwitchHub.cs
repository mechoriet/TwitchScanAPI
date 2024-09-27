using System.Threading.Tasks;
using TwitchScanAPI.Models;
using TwitchScanAPI.Models.Twitch;

namespace TwitchScanAPI.Hubs
{
    public interface ITwitchHub
    {
        Task ReceiveChannelMessage(ChannelMessage message);
        Task ReceiveElevatedMessage(ChannelMessage message);
        Task ReceiveObservedMessage(ChannelMessage message);
        Task ReceiveBannedUser(BannedUser bannedUser);
        Task ReceiveTimedOutUser(TimedOutUser timedOutUser);
        Task ReceiveClearedMessage(ClearedMessage clearedMessage);
        Task ReceiveSubscription(Subscription subscription);
        Task ReceiveRaidEvent(RaidEvent raidEvent);
        Task ReceiveHostEvent(HostEvent hostEvent);
        Task ReceiveUserJoined(string username, string channel);
        Task ReceiveUserLeft(string username);
    }

}