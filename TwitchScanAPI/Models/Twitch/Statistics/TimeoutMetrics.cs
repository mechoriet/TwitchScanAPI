using System.Collections.Generic;
using System.Text.Json.Serialization;
using TwitchScanAPI.Models.Twitch.Base;

namespace TwitchScanAPI.Models.Twitch.Statistics;

public class TimeoutMetrics : TimedEntity
{
    public long TotalTimeouts { get; set; }
    public long TotalTimeoutDuration { get; set; }
    public double AverageTimeoutDuration { get; set; }
    public IEnumerable<TimeoutReasonResult> TimeoutReasons { get; set; } = [];
}
