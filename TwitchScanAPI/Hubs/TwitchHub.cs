using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace TwitchScanAPI.Hubs
{
    public class TwitchHub : Hub<ITwitchHub>
    {
        public async Task JoinChannel(string channelName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, channelName);
        }

        public async Task LeaveChannel(string channelName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, channelName);
        }
    }

}