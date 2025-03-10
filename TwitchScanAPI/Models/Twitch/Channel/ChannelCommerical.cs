namespace TwitchScanAPI.Models.Twitch.Channel;

public class ChannelCommercial(string channelName, int length)
{
    public string ChannelName { get; set; } = channelName;
    
    public int Length { get; set; } = length;
}