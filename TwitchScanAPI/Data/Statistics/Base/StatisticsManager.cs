using System.Collections.Generic;

namespace TwitchScanAPI.Data.Statistics.Base
{
    public class StatisticsManager
    {
        private readonly Statistics _statistics = new();
        public bool PropagateEvents { get; set; } = true;
        
        public void Reset()
        {
            _statistics.Reset();
        }

        public void Update<TEvent>(TEvent eventData)
        {
            if (!PropagateEvents) return;
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