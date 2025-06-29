using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Channel;
using TwitchScanAPI.Models.Twitch.Statistics;
using TwitchScanAPI.Services;

namespace TwitchScanAPI.Data.Statistics.Channel
{
    public readonly struct ViewerEntry(uint timestampSeconds, int viewers)
    {
        // Unix timestamp (4 bytes)
        public readonly int Viewers = viewers;           // Viewer count (4 bytes)

        // Convert back to DateTime when needed
        public DateTime GetDateTime() => DateTimeOffset.FromUnixTimeSeconds(timestampSeconds).DateTime;
    
        // Constructor from DateTime
        public ViewerEntry(DateTime timestamp, int viewers) 
            : this((uint)((DateTimeOffset)timestamp).ToUnixTimeSeconds(), viewers)
        {
        }
    }
    public class ChannelMetricsStatistic : StatisticBase
    {
        public override string Name => "ChannelMetrics";
        private const int BucketSize = 1; // Grouping viewers into 1-minute periods

        // For Viewer Count Tracking
        private ConcurrentQueue<ViewerEntry> _viewerHistory = new();

        // For Viewers Over Time
        private ConcurrentDictionary<string, long> _viewersOverTime = new();

        // For Current Game and Uptime Tracking
        private string? _currentGame;
        private TimeSpan _currentUptime;
        private long _peakViewers;
        private long _totalViewers;
        private long _viewerCountEntries;
        private Trend _trend;

        protected override object ComputeResult()
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
            // moved trend calculation to update instead of compute to not always run it

            return ChannelMetrics.Create(
                currentViewers,
                (int)Math.Round(averageViewers),
                (int)peakViewersSnapshot,
                _currentGame ?? string.Empty,
                _currentUptime,
                _viewersOverTime.ToDictionary(kv => DateTime.Parse(kv.Key), kv => kv.Value),
                totalWatchTimeHours,
                _trend
            );
        }

        public Task Update(ChannelInformation channelInfo)
        {
            var currentTime = DateTime.UtcNow;

            // Update Viewer History Queue
            _viewerHistory.Enqueue(new ViewerEntry(DateTime.Now, (int)channelInfo.Viewers));
            Interlocked.Add(ref _totalViewers, channelInfo.Viewers);
            Interlocked.Increment(ref _viewerCountEntries);

            // Update Peak Viewers if the new value exceeds the current peak
            UpdatePeakViewers(channelInfo.Viewers);

            // Add viewers to the dictionary that tracks viewers over time
            UpdateViewersOverTime(currentTime, channelInfo.Viewers);

            // Update the currently active game if it's different
            if (!string.IsNullOrWhiteSpace(channelInfo.Game))
                _currentGame = channelInfo.Game.Trim();

            // Update the uptime based on the channel's reported start time
            _currentUptime = currentTime - channelInfo.Uptime;
            
            _trend = TrendService.CalculateTrend(
                _viewerHistory,
                d => d.Viewers,
                d => d.GetDateTime()
            );
            HasUpdated = true;
            return Task.CompletedTask;
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
            // Round the timestamp to the nearest minute
            var roundedMinutes = Math.Floor((double)timestamp.Minute / BucketSize) * BucketSize;
            var roundedTime = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour,
                    (int)roundedMinutes, 0)
                .ToUniversalTime()
                .ToString("yyyy-MM-ddTHH:mm:ssZ");

            // Add or update the viewer count for the time bucket
            _viewersOverTime.AddOrUpdate(roundedTime, viewers, (_, value) => viewers > value ? viewers : value);
        }

        public override void Dispose()
        {
            base.Dispose();
            _viewerHistory.Clear();
            _viewersOverTime.Clear();
            _currentGame = null;
            _currentUptime = TimeSpan.Zero;
            _peakViewers = 0;
            _totalViewers = 0;
            _viewerCountEntries = 0;
            _trend = Trend.Stable;
        }
    }
}