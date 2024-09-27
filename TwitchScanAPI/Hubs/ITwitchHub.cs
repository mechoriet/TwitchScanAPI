using System.Threading.Tasks;
using TwitchScanAPI.Models;

namespace TwitchScanAPI.Hubs
{
    public interface ITwitchHub
    {
        Task ReceiveChannelMessage(ChannelMessage message);
        Task ReceiveElevatedMessage(ChannelMessage message);
        Task ReceiveBannedUser(object bannedUser);
        Task ReceiveTimedOutUser(object timedOutUser);
        Task ReceiveClearedMessage(object clearedMessage);
        Task ReceiveSubscription(object subscription);
        Task ReceiveUserJoined(string username, string channel);
        Task ReceiveUserLeft(string username);
    }
}