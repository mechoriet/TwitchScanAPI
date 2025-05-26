using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat;

public class DeletedMessagesStatistic: StatisticBase
{
    public override string Name => "TotalDeletedMessages";

    private int _clearedMessagesCount;

    protected override object ComputeResult()
    {
        return _clearedMessagesCount;
    }

    public Task Update(ClearedMessage eventData)
    {
        _clearedMessagesCount++;
        // Implement your logic to update the statistic based on event data
        HasUpdated = true;
        return Task.CompletedTask;
    }
    
    public override void Dispose()
    {
        base.Dispose();
        _clearedMessagesCount = 0;
    }
}