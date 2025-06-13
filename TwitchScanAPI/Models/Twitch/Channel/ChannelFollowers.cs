namespace TwitchScanAPI.Models.Twitch.Channel;

public class ChannelFollowers(string channelName, int count, bool force)
{
    public string ChannelName { get; set; } = channelName;
    
    public int Count { get; set; } = count;

    public bool Partial = force;
}