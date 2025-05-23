﻿using System;
using System.Collections.Generic;
using System.Linq;
using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.Statistics
{
    public enum Trend
    {
        Increasing,
        Decreasing,
        Stable
    }

    public class ViewerStatistics : TimedEntity
    {
        public long CurrentViewers { get; set; }
        public long AverageViewers { get; set; }
        public long PeakViewers { get; set; }
    }

    public class ChannelMetrics : TimedEntity
    {
        public ViewerStatistics ViewerStatistics { get; private set; } = new();
        public string CurrentGame { get; private set; } = string.Empty;
        public TimeSpan Uptime { get; private set; }
        public Dictionary<string, long> ViewersOverTime { get; private set; } = new();
        public double TotalWatchTime { get; private set; }
        public Trend Trend { get; private set; }

        public static ChannelMetrics Create(
            long currentViewers,
            long averageViewers,
            long peakViewersSnapshot,
            string currentGame,
            TimeSpan currentUptime,
            Dictionary<DateTime, long> viewersOverTime,
            double totalWatchTime,
            Trend trend)
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
                ViewersOverTime =
                    viewersOverTime
                        .OrderByDescending(kv => kv.Key)
                        .ToDictionary(kv => kv.Key.ToString("yyyy-MM-ddTHH:mm:ssZ"), kv => kv.Value),
                TotalWatchTime = totalWatchTime,
                Trend = trend
            };
        }
    }
}