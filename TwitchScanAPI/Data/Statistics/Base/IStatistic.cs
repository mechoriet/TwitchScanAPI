namespace TwitchScanAPI.Data.Statistics.Base
{
    public interface IStatistic
    {
        string Name { get; }
        object GetResult();
    }
}