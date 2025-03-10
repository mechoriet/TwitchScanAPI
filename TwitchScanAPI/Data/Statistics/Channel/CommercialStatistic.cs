using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Channel;
using TwitchScanAPI.Models.Twitch.Statistics;

namespace TwitchScanAPI.Data.Statistics.Channel
{
    public class CommercialStatistic : StatisticBase
    {
        private const int BucketSize = 1; // Group commercials into 1-minute periods

        // Tracks the total duration of commercials per channel
        private ConcurrentDictionary<string, int> _commercialDurations = new(StringComparer.OrdinalIgnoreCase);

        // Tracks commercials over time (bucketed by minute)
        private ConcurrentDictionary<string, List<ChannelCommercial>> _commercialsOverTime = new();

        public override string Name => "CommercialStatistic";

        protected override object ComputeResult()
        {
            // Aggregate commercials over time, ordered chronologically
            var commercialsOverTime = _commercialsOverTime
                .OrderBy(kvp => kvp.Key)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Return the result with all necessary metrics
            return new CommercialStatisticResult
            {
                TotalCommercialTime = _commercialDurations.Values.Sum(),
                CommercialsOverTime = commercialsOverTime
            };
        }

        public Task Update(ChannelCommercial commercial)
        {
            // Increment total commercial duration for the channel
            _commercialDurations.AddOrUpdate(
                commercial.ChannelName,
                commercial.Length,
                (_, totalLength) => totalLength + commercial.Length
            );

            // Track the commercial over time (batched by minute)
            var currentTime = DateTime.UtcNow;
            UpdateCommercialsOverTime(currentTime, commercial);
            HasUpdated = true;
            return Task.CompletedTask;
        }

        private void UpdateCommercialsOverTime(DateTime timestamp, ChannelCommercial commercial)
        {
            // Round the timestamp to the nearest minute
            var roundedMinutes = Math.Floor((double)timestamp.Minute / BucketSize) * BucketSize;
            var roundedTime = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour,
                    (int)roundedMinutes, 0)
                .ToString("yyyy-MM-ddTHH:mm:ssZ");

            // Add the commercial to the bucket for the given time
            _commercialsOverTime.AddOrUpdate(
                roundedTime,
                [commercial],
                (_, list) =>
                {
                    list.Add(commercial);
                    return list;
                }
            );
        }

        public override void Dispose()
        {
            base.Dispose();
            _commercialDurations = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _commercialsOverTime = new ConcurrentDictionary<string, List<ChannelCommercial>>();
        }
    }
}