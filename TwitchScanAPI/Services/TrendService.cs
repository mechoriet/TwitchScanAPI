using System;
using System.Collections.Generic;
using System.Linq;
using TwitchScanAPI.Models.Twitch.Statistics;

namespace TwitchScanAPI.Services
{
    public static class TrendService
    {
        private static readonly TimeSpan DefaultTrendTimeSpan = TimeSpan.FromMinutes(30);

        /// <summary>
        ///     Calculates the trend by comparing the average of recent data points within a timespan
        ///     with the average of the entire dataset. If the timespan exceeds the total data range,
        ///     only the entire dataset is considered.
        /// </summary>
        /// <typeparam name="T">The type of data points.</typeparam>
        /// <param name="data">The collection of data points.</param>
        /// <param name="getValue">Function to extract the value from a data point.</param>
        /// <param name="timeSpan">The time span to consider for the recent trend calculation.</param>
        /// <param name="getTime">Function to extract the time from a data point.</param>
        /// <returns>The calculated trend.</returns>
        public static Trend CalculateTrend<T>(
            IEnumerable<T> data, // Must be sorted by getTime
            Func<T, double> getValue,
            Func<T, DateTime> getTime,
            TimeSpan timeSpan = default)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            timeSpan = timeSpan == default ? DefaultTrendTimeSpan : timeSpan;
            double sum = 0, recentSum = 0;
            int totalCount = 0, recentCount = 0;
            DateTime? firstTime = null, lastTime = null;
            double? lastValue = null;
            var thresholdTime = DateTime.UtcNow - timeSpan;

            foreach (var item in data)
            {
                var value = getValue(item);
                var time = getTime(item);

                sum += value;
                totalCount++;

                if (!firstTime.HasValue) firstTime = time;
                lastTime = time;
                lastValue = value;

                if (time >= thresholdTime)
                {
                    recentSum += value;
                    recentCount++;
                }
            }

            if (totalCount == 0) return Trend.Stable;

            var totalDataRange = lastTime.Value - firstTime.Value;
            var overallAverage = sum / totalCount;

            if (timeSpan >= totalDataRange)
            {
                return lastValue > overallAverage ? Trend.Increasing :
                    lastValue < overallAverage ? Trend.Decreasing : Trend.Stable;
            }

            if (recentCount == 0) return Trend.Stable;

            var recentAverage = recentSum / recentCount;
            return recentAverage > overallAverage ? Trend.Increasing :
                recentAverage < overallAverage ? Trend.Decreasing : Trend.Stable;
        }
    }
}