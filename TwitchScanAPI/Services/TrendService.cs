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
            IEnumerable<T> data,
            Func<T, double> getValue,
            Func<T, DateTime> getTime,
            TimeSpan timeSpan = default)
        {
            var dataList = data.OrderBy(getTime).ToList();
            if (dataList.Count == 0) return Trend.Stable;

            timeSpan = timeSpan == default ? DefaultTrendTimeSpan : timeSpan;
            var firstTime = getTime(dataList.First());
            var lastTime = getTime(dataList.Last());
            var totalDataRange = lastTime - firstTime;

            double sum = 0, recentSum = 0;
            int totalCount = dataList.Count, recentCount = 0;
            var thresholdTime = DateTime.UtcNow - timeSpan;

            // Iterate through the data once, calculating both overall and recent sums
            foreach (var item in dataList)
            {
                var value = getValue(item);
                var time = getTime(item);

                sum += value;
                if (time < thresholdTime) continue;
                recentSum += value;
                recentCount++;
            }

            // Calculate overall average
            var overallAverage = sum / totalCount;

            // If timespan exceeds data range, compare the last point with overall average
            if (timeSpan >= totalDataRange)
            {
                var lastValue = getValue(dataList.Last());
                return lastValue > overallAverage ? Trend.Increasing :
                    lastValue < overallAverage ? Trend.Decreasing : Trend.Stable;
            }

            // If there are no recent data points, return stable
            if (recentCount == 0) return Trend.Stable;

            // Calculate recent average and compare it with the overall average
            var recentAverage = recentSum / recentCount;
            return recentAverage > overallAverage ? Trend.Increasing :
                recentAverage < overallAverage ? Trend.Decreasing : Trend.Stable;
        }
    }
}