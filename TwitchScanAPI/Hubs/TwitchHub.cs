using Microsoft.AspNetCore.SignalR;

namespace TwitchScanAPI.Hubs
{
    public class TwitchHub : Hub<ITwitchHub>
    {
        public void JoinChannel(string channelName)
        {
            Groups.AddToGroupAsync(Context.ConnectionId, channelName);
        }
        
        public void LeaveChannel(string channelName)
        {
            Groups.RemoveFromGroupAsync(Context.ConnectionId, channelName);
        }
    }
}