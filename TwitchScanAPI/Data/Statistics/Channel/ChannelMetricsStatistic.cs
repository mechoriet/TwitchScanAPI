using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Channel;
using Timer = System.Timers.Timer;

namespace TwitchScanAPI.Data.Statistics.Channel
{
    public class ViewerStatistics
    {
        public long CurrentViewers { get; set; }
        public long AverageViewers { get; set; }
        public long PeakViewers { get; set; }
    }

    public class ChannelMetrics
    {
        public ViewerStatistics ViewerStatistics { get; private set; } = new();
        public string CurrentGame { get; private set; } = string.Empty;
        public TimeSpan Uptime { get; private set; }
        public Dictionary<DateTime, long> ViewersOverTime { get; private set; } = new();

        public static ChannelMetrics Create(
            long currentViewers,
            long averageViewers,
            long peakViewersSnapshot,
            string currentGame,
            TimeSpan currentUptime,
            Dictionary<DateTime, long> viewersOverTime)
        {
            return new ChannelMetrics
            {
                ViewerStatistics = new ViewerStatistics
                {
                    CurrentViewers = currentViewers,
                    AverageViewers = averageViewers,
                    PeakViewers = peakViewersSnapshot
                },
                CurrentGame = currentGame,
                Uptime = currentUptime,
                ViewersOverTime = viewersOverTime
                    .OrderByDescending(kv => kv.Key)
                    .ToDictionary(kv => kv.Key, kv => kv.Value)
            };
        }
    }

    public class ChannelMetricsStatistic : IStatistic
    {
        public string Name => "ChannelMetrics";

        // For Viewer Count Tracking
        private readonly ConcurrentQueue<(DateTime Timestamp, long Viewers)> _viewerHistory = new();
        private long _peakViewers;
        private long _totalViewers;
        private long _viewerCountEntries;

        // For Current Game and Uptime Tracking
        private string? _currentGame;
        private TimeSpan _currentUptime;

        // For Viewers Over Time
        private readonly ConcurrentDictionary<string, long> _viewersOverTime = new();
        private readonly TimeSpan _retentionPeriod = TimeSpan.FromHours(24);
        private const int BucketSize = 1; // Grouping viewers into 1-minute periods
        private readonly Timer _cleanupTimer;

        public ChannelMetricsStatistic()
        {
            // Initialize the timer to trigger cleanup every hour
            _cleanupTimer = new Timer(3600000); // 3600000 ms = 1 hour
            _cleanupTimer.Elapsed += (_, _) => CleanupOldData();
            _cleanupTimer.AutoReset = true;
            _cleanupTimer.Start();
        }

        public object GetResult()
        {
            // Calculate Viewer Statistics
            var averageViewers = _viewerCountEntries == 0 ? 0 : (double)_totalViewers / _viewerCountEntries;
            var peakViewersSnapshot = Interlocked.Read(ref _peakViewers);

            // Get the most recent viewers count
            var currentViewers = _viewerHistory.LastOrDefault().Viewers;

            // Return the result with all necessary metrics
            return ChannelMetrics.Create(
                currentViewers,
                (int)Math.Round(averageViewers),
                (int)peakViewersSnapshot,
                _currentGame ?? string.Empty,
                _currentUptime,
                _viewersOverTime.ToDictionary(kv => DateTime.Parse(kv.Key), kv => kv.Value)
            );
        }

        public void Update(ChannelInformation channelInfo)
        {
            var currentTime = DateTime.UtcNow;

            // Update Viewer History
            _viewerHistory.Enqueue((currentTime, channelInfo.Viewers));
            Interlocked.Add(ref _totalViewers, channelInfo.Viewers);
            Interlocked.Increment(ref _viewerCountEntries);

            // Update Peak Viewers
            UpdatePeakViewers(channelInfo.Viewers);

            // Add viewers over time
            UpdateViewersOverTime(currentTime, channelInfo.Viewers);

            // Clean up old data
            TrimQueue(_viewerHistory, TimeSpan.FromHours(24), currentTime);

            // Update Current Game
            if (!string.IsNullOrWhiteSpace(channelInfo.Game))
            {
                _currentGame = channelInfo.Game.Trim();
            }

            // Update Current Uptime
            _currentUptime = DateTime.UtcNow - channelInfo.Uptime;
        }

        private void UpdatePeakViewers(long currentViewers)
        {
            long initialValue, computedValue;
            do
            {
                initialValue = Interlocked.Read(ref _peakViewers);
                if (currentViewers <= initialValue)
                    break;
                computedValue = currentViewers;
            } while (computedValue != Interlocked.CompareExchange(ref _peakViewers, computedValue, initialValue));
        }

        private void UpdateViewersOverTime(DateTime timestamp, long viewers)
        {
            // Round the timestamp to the nearest 5-minute bucket
            var roundedMinutes = Math.Floor((double)timestamp.Minute / BucketSize) * BucketSize;
            var roundedTime = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour,
                    (int)roundedMinutes, 0)
                .ToString("yyyy-MM-ddTHH:mm:ssZ");

            // Add or update the viewer count for the time bucket
            _viewersOverTime.AddOrUpdate(roundedTime, viewers, (_, _) => viewers);
        }

        private void CleanupOldData()
        {
            // Calculate the expiration time
            var expirationTime = DateTime.UtcNow.Subtract(_retentionPeriod);

            // List to hold the keys that should be removed
            var keysToRemove = new List<string>();

            foreach (var key in _viewersOverTime.Keys)
            {
                if (DateTime.TryParse(key, out var timeKey) && timeKey < expirationTime)
                {
                    keysToRemove.Add(key);
                }
            }

            // Remove expired entries
            foreach (var key in keysToRemove)
            {
                _viewersOverTime.TryRemove(key, out _);
            }
        }

        private void TrimQueue(ConcurrentQueue<(DateTime Timestamp, long Value)> queue, TimeSpan maxAge,
            DateTime currentTime)
        {
            while (queue.TryPeek(out var entry))
            {
                if ((currentTime - entry.Timestamp) > maxAge)
                {
                    queue.TryDequeue(out _);
                }
                else
                {
                    break;
                }
            }
        }
    }
}