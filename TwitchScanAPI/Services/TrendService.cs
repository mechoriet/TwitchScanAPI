using System;
using System.Collections.Generic;
using System.Linq;
using TwitchScanAPI.Models.Twitch.Statistics;

namespace TwitchScanAPI.Services
{
    public static class TrendService
    {
        private static readonly TimeSpan TrendTimeSpan = TimeSpan.FromMinutes(30);
        
        /// <summary>
        /// Calculates the trend based on the provided data.
        /// </summary>
        /// <typeparam name="T">The type of data points.</typeparam>
        /// <param name="data">The collection of data points.</param>
        /// <param name="getValue">Function to extract the value from a data point.</param>
        /// <param name="timeSpan">The time span to consider for the trend calculation.</param>
        /// <param name="getTime">Function to extract the time from a data point.</param>
        /// <returns>The calculated trend.</returns>
        public static Trend CalculateTrend<T>(
            IEnumerable<T> data,
            Func<T, double> getValue,
            Func<T, DateTime> getTime,
            TimeSpan timeSpan = default)
        {
            timeSpan = timeSpan == default ? TrendTimeSpan : timeSpan;
            var dataList = data
                .Where(d => getTime(d) >= DateTime.UtcNow - timeSpan)
                .ToList();
            if (!dataList.Any()) return Trend.Stable;

            // Get the last data point's value
            var currentValue = getValue(dataList.Last());

            // Calculate the average of all data points
            var averageValue = dataList.Average(getValue);

            // Compare the last value with the average
            if (currentValue > averageValue) return Trend.Increasing;
            return currentValue < averageValue ? Trend.Decreasing : Trend.Stable;
        }
    }
}