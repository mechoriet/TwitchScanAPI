using System.Collections.Generic;

namespace TwitchScanAPI.Data.Statistics.Base
{
    public class StatisticsManager
    {
        private readonly Statistics _statistics = new();

        public void Update<TEvent>(TEvent eventData)
        {
            _statistics.Update(eventData);
        }

        public IDictionary<string, object> GetAllStatistics()
        {
            return _statistics.GetAllStatistics();
        }

        public object? GetStatistic(string name)
        {
            return _statistics.GetStatistic(name);
        }
    }
}