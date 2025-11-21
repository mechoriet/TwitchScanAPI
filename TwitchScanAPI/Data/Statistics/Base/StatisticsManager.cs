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
        private static readonly Histogram StatisticsComputationDuration = Metrics.CreateHistogram(
            "twitch_statistics_computation_duration_seconds",
            "Statistics computation duration",
            new HistogramConfiguration
            {
                LabelNames = ["statistic"],
                Buckets =
                [
                    0.0001,  // 0.1 ms
                    0.00025,
                    0.0005,
                    0.001,
                    0.0025,
                    0.005,
                    0.01,
                    0.025,
                    0.05,
                    0.1,
                    0.25,
                    0.5,
                    1,
                    2.5,
                    5,
                    10
                ]
            }
        );
        private static readonly Counter StatisticsUpdateCount = Metrics.CreateCounter("twitch_statistics_update_count", "Number of statistics updates", "event_type");
        private static readonly Gauge StatisticsActiveHandlers = Metrics.CreateGauge("twitch_statistics_active_handlers", "Number of active statistics handlers", "event_type");

        public void Reset()
        {
            _statistics.Reset();
        }

        public async Task Update<TEvent>(TEvent eventData)
        {
            if (!PropagateEvents) return;

            var eventTypeName = typeof(TEvent).Name;
            StatisticsUpdateCount.WithLabels(eventTypeName).Inc();

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await _statistics.Update(eventData);
                StatisticsComputationDuration.WithLabels(eventTypeName).Observe(stopwatch.Elapsed.TotalSeconds);
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

            if (string.IsNullOrEmpty(username)) return chatHistory.AsParallel().OrderBy(x => x.Time).ToList();

            return chatHistory
                .AsParallel()
                .Where(x => string.Equals(x.Username, username, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(x => x.Time)
                .ToList();
        }
    }
}