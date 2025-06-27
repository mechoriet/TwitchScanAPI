using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Client.Models;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Statistics;

namespace TwitchScanAPI.Data.Statistics.Channel
{
    public class RaidStatistic : StatisticBase
    {
        private const int BucketSize = 1; // Grouping raids into 1-minute periods

        // Tracks the count of raids
        private readonly ConcurrentDictionary<string, int> _raidCounts = new(StringComparer.OrdinalIgnoreCase);

        // Tracks raids over time (bucketed by minute)
        private readonly ConcurrentDictionary<string, string> _raidsOverTime = new();

        public override string Name => "RaidStatistic";

        protected override object ComputeResult()
        {
            // Aggregate raids over time, ordered chronologically
            var raidsOverTime = _raidsOverTime
                .OrderBy(kvp => kvp.Key)
                .ToList();

            // Return the result with all necessary metrics
            return new RaidStatisticResult
            {
                TotalRaids = _raidCounts.Values.Sum(),
                TopRaiders = _raidCounts
                    .OrderByDescending(kv => kv.Value)
                    .Take(10)
                    .ToDictionary(kv => kv.Key, kv => kv.Value),
                RaidsOverTime = raidsOverTime
            };
        }

        public Task Update(RaidNotification raidNotification)
        {
            var username = raidNotification.MsgParamLogin;
            // Increment raid count for the raider
            if (int.TryParse(raidNotification.MsgParamViewerCount, out var viewerCount))
            {
                _raidCounts.AddOrUpdate(
                    username,
                    viewerCount,
                    (_, count) => count + viewerCount
                );
            }
            else
            {
                _raidCounts.AddOrUpdate(
                    username,
                    1,
                    (_, count) => count + 1
                );
            }

            // Track the raid over time (batched by minute)
            var currentTime = DateTime.UtcNow;
            UpdateRaidsOverTime(currentTime, raidNotification.MsgParamLogin);
            HasUpdated = true;
            return Task.CompletedTask;
        }

        private void UpdateRaidsOverTime(DateTime timestamp, string username)
        {
            // Round the timestamp to the nearest minute
            var roundedMinutes = Math.Floor((double)timestamp.Minute / BucketSize) * BucketSize;
            var roundedTime = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour,
                    (int)roundedMinutes, 0)
                .ToString("yyyy-MM-ddTHH:mm:ssZ");

            // Add or update the raid bucket (here we simply store the username for the given time bucket)
            _raidsOverTime.AddOrUpdate(roundedTime, username, (_, _) => username);
        }

        public override void Dispose()
        {
            base.Dispose();
            _raidCounts.Clear();
            _raidsOverTime.Clear();
        }
    }
}