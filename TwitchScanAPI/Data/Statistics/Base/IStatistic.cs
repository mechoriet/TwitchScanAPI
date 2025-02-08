using System;

namespace TwitchScanAPI.Data.Statistics.Base
{
    public interface IStatistic : IDisposable
    {
        string Name { get; }
        object GetResult();
    }
}