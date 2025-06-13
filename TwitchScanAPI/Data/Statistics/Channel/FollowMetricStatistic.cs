using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Channel;

namespace TwitchScanAPI.Data.Statistics.Channel;

public class FollowMetricStatistic : StatisticBase
{
    public override string Name => "FollowMetrics";

    // Round timestamps to the nearest N-minute boundary for visual clarity
    private const int BucketSize = 1; // in minutes
    private ConcurrentDictionary<DateTime, long> _followers = new();
    private long _lastValue;
    protected override object ComputeResult()
    {
        return _followers
            .OrderBy(kv => kv.Key)
            .Select(kv => new
            {
                Time = kv.Key,
                TotalFollowers = kv.Value
            })
            .ToDictionary(kv => kv.Time.ToString("yyyy-MM-ddTHH:mm:ssZ"), kv => kv.TotalFollowers);
    }

    public Task Update(ChannelFollowers channelFollowers)
    {
        var now = DateTime.UtcNow;

        // Round current time to the nearest BucketSize-minute boundary
        var roundedMinutes = Math.Floor((double)now.Minute / BucketSize) * BucketSize;
        var roundedTime = new DateTime(
            now.Year,
            now.Month,
            now.Day,
            now.Hour,
            (int)roundedMinutes,
            0
        );

        // Only update if count has changed or this is a partial fetch (start/end of list)
        if (_lastValue == channelFollowers.Count && !channelFollowers.Partial) return Task.CompletedTask;
        _lastValue = channelFollowers.Count;

        // Replace or insert the latest follower count at the rounded timestamp
        _followers.AddOrUpdate(roundedTime,
            channelFollowers.Count,
            (_, _) => channelFollowers.Count);

        return Task.CompletedTask;
    }
    
    public override void Dispose()
    {
        base.Dispose();
        _followers = new ConcurrentDictionary<DateTime, long>();
        _lastValue = 0;
    }
}