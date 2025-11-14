using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Prometheus;
using TwitchScanAPI.Models.Twitch.Statistics;

namespace TwitchScanAPI.Data.Statistics.Base
{
    public class StatisticsManager
    {
        private readonly Statistics _statistics = new();
        public bool PropagateEvents { get; set; } = true;

        // Prometheus metrics
        private static readonly Histogram StatisticsComputationDuration = Metrics.CreateHistogram("twitch_statistics_computation_duration_seconds", "Statistics computation duration", "statistic");

        public void Reset()
        {
            _statistics.Reset();
        }

        public async Task Update<TEvent>(TEvent eventData)
        {
            if (!PropagateEvents) return;

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await _statistics.Update(eventData);
                StatisticsComputationDuration.WithLabels(typeof(TEvent).Name).Observe(stopwatch.Elapsed.TotalSeconds);
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public Dictionary<string, object?> GetAllStatistics()
        {
            return _statistics.GetAllStatistics();
        }

        public object? GetStatistic(string name)
        {
            return _statistics.GetStatistic(name);
        }

        public List<ChatHistory> GetChatHistory(string? username = null)
        {
            var chatHistory = _statistics.GetStatistic("ChatHistory") as List<ChatHistory> ?? new List<ChatHistory>();

            if (string.IsNullOrEmpty(username)) return chatHistory.OrderBy(x => x.Time).ToList();

            return chatHistory
                .Where(x => string.Equals(x.Username, username, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(x => x.Time)
                .ToList();
        }
    }
}