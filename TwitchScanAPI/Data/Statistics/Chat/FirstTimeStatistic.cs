using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat;

public class FirstTimeStatistic: StatisticBase
{
    public override string Name => "FirstTimeChatter";

    private int _newChattersCount;

    protected override object ComputeResult()
    {
        return _newChattersCount;
    }

    public Task Update(ChannelMessage eventData)
    {
        if (eventData.ChatMessage.FirstTime)
        {
            _newChattersCount++;
        }
        // Implement your logic to update the statistic based on event data
        HasUpdated = true;
        return Task.CompletedTask;
    }
    
    public override void Dispose()
    {
        base.Dispose();
        _newChattersCount = 0;
    }
}