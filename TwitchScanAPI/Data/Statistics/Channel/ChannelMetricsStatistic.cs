using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Channel;
using TwitchScanAPI.Models.Twitch.Statistics;
using TwitchScanAPI.Services;
using Timer = System.Timers.Timer;

namespace TwitchScanAPI.Data.Statistics.Channel
{
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
        private readonly TimeSpan _retentionPeriod = TimeSpan.FromHours(48);
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

        /// <summary>
        /// Gets the result of all tracked metrics, including viewers, watch time, and game/uptime information.
        /// </summary>
        public object GetResult()
        {
            // Calculate average viewers if there are entries, else set to 0
            var averageViewers = _viewerCountEntries == 0 ? 0 : (double)_totalViewers / _viewerCountEntries;

            // Snapshot of the peak viewers
            var peakViewersSnapshot = Interlocked.Read(ref _peakViewers);

            // Get the most recent viewers count
            var currentViewers = _viewerHistory.LastOrDefault().Viewers;

            // Convert total watch time to hours for easier interpretation
            var totalWatchTimeHours = _viewersOverTime.Values.Sum() / 60.0;
            
            // Calculate the trend
            var viewerData = _viewerHistory.ToList();
            var trend = TrendService.CalculateTrend(
                viewerData,
                d => d.Viewers,
                d => d.Timestamp
            );

            // Return all metrics, including watch time over time
            return ChannelMetrics.Create(
                currentViewers,
                (int)Math.Round(averageViewers),
                (int)peakViewersSnapshot,
                _currentGame ?? string.Empty,
                _currentUptime,
                _viewersOverTime.ToDictionary(kv => DateTime.Parse(kv.Key), kv => kv.Value),
                totalWatchTimeHours,
                trend
            );
        }

        /// <summary>
        /// Updates the channel metrics with new channel information, including viewers, uptime, and game.
        /// This method is called each time new data is fetched from the channel.
        /// </summary>
        public void Update(ChannelInformation channelInfo)
        {
            var currentTime = DateTime.UtcNow;

            // Update Viewer History Queue
            _viewerHistory.Enqueue((currentTime, channelInfo.Viewers));
            Interlocked.Add(ref _totalViewers, channelInfo.Viewers);
            Interlocked.Increment(ref _viewerCountEntries);

            // Update Peak Viewers if the new value exceeds the current peak
            UpdatePeakViewers(channelInfo.Viewers);

            // Add viewers to the dictionary that tracks viewers over time
            UpdateViewersOverTime(currentTime, channelInfo.Viewers);

            // Clean up old data beyond the retention period (48 hours)
            TrimQueue(_viewerHistory, TimeSpan.FromHours(48), currentTime);

            // Update the currently active game if it's different
            if (!string.IsNullOrWhiteSpace(channelInfo.Game))
            {
                _currentGame = channelInfo.Game.Trim();
            }

            // Update the uptime based on the channel's reported start time
            _currentUptime = currentTime - channelInfo.Uptime;
        }

        /// <summary>
        /// Tracks the peak viewers by comparing the current viewer count with the existing peak.
        /// Updates the peak if the current viewers exceed the peak.
        /// </summary>
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

        /// <summary>
        /// Tracks the number of viewers over time, using rounded minute-based buckets.
        /// Updates the dictionary with the viewer count for each time bucket.
        /// </summary>
        private void UpdateViewersOverTime(DateTime timestamp, long viewers)
        {
            // Round the timestamp to the nearest minute
            var roundedMinutes = Math.Floor((double)timestamp.Minute / BucketSize) * BucketSize;
            var roundedTime = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour,
                    (int)roundedMinutes, 0)
                .ToString("yyyy-MM-ddTHH:mm:ssZ");

            // Add or update the viewer count for the time bucket
            _viewersOverTime.AddOrUpdate(roundedTime, viewers, (_, _) => viewers);
        }

        /// <summary>
        /// Cleans up data older than the retention period (48 hours).
        /// This ensures the history and metrics remain relevant and do not consume too much memory.
        /// </summary>
        private void CleanupOldData()
        {
            var expirationTime = DateTime.UtcNow.Subtract(_retentionPeriod);

            // List to hold keys that need to be removed
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

        /// <summary>
        /// Trims the queue to remove data older than the max allowed age.
        /// This prevents the queue from growing indefinitely.
        /// </summary>
        private static void TrimQueue(ConcurrentQueue<(DateTime Timestamp, long Value)> queue, TimeSpan maxAge,
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