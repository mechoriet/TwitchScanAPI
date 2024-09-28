using TwitchScanAPI.Models.Twitch;

namespace TwitchScanAPI.Data.Statistics.Chat.Base
{
    public interface IStatistic
    {
        string Name { get; }
        object GetResult();
    }
}