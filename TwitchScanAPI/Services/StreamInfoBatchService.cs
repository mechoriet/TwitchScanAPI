using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;
using TwitchScanAPI.Global;

namespace TwitchScanAPI.Services;

public class StreamInfoBatchService
{
    private readonly Lock _lock = new();
    private static readonly HashSet<string> PendingChannels = [];
    private readonly ConcurrentDictionary<string, TaskCompletionSource<Stream?>> _responseMap = new();
    private static Timer _timer;
    private static readonly TwitchAPI Api = new();
    private readonly IConfiguration _configuration;

    public StreamInfoBatchService(IConfiguration configuration)
    {
        _timer = new Timer(_ => ProcessBatch(), null, Timeout.Infinite, Timeout.Infinite);
        _configuration = configuration;
        ConfigureTwitchApi();
    }
    
    private void ConfigureTwitchApi()
    {
        Api.Settings.ClientId = _configuration.GetValue<string>(Variables.TwitchClientId);
        Api.Settings.Secret = _configuration.GetValue<string>(Variables.TwitchClientSecret);
    }

    public Task<Stream?> RequestRawStreamAsync(string channelName)
    {
        lock (_lock)
        {
            var tcs = _responseMap.GetOrAdd(channelName, _ => new TaskCompletionSource<Stream?>());
        
            // Only add to pending if this is a new entry
            if (!PendingChannels.Add(channelName)) return tcs.Task;
            var pendingCount = PendingChannels.Count;
            var delay = GetAdaptiveDelay(pendingCount);
            _timer.Change(delay, Timeout.Infinite);

            return tcs.Task;
        }
    }
    
    private double _pendingEma;
    private const double EmaAlpha = 0.3; // smoothing factor (lower = smoother)
    private int GetAdaptiveDelay(int currentPendingCount)
    {
        // Update EMA
        _pendingEma = EmaAlpha * currentPendingCount + (1 - EmaAlpha) * _pendingEma;

        // Map EMA (1–20) to delay (1200–300 ms)
        const double minDelay = 300;
        const double maxDelay = 1200;
        var clampedEma = Math.Clamp(_pendingEma, 1, 20);

        // Invert: more requests = shorter delay
        var scale = (clampedEma - 1) / (20 - 1); // range: 0–1
        var delay = (int)(maxDelay - scale * (maxDelay - minDelay));
        /*Console.WriteLine($"[Debounce] Pending={currentPendingCount}, EMA={_pendingEma:F2}, Delay={delay}ms");*/
        return delay;
    }
    private async void ProcessBatch()
    {
        List<string> batch;

        lock (_lock)
        {
            batch = PendingChannels.Take(100).ToList();
            foreach (var name in batch)
                PendingChannels.Remove(name);
        }
        if (batch.Count == 0)
            return;
        //Console.WriteLine($"[BatchService] Processing {batch.Count} channels: [{string.Join(", ", batch)}]");
        try
        {
            var response = await Api.Helix.Streams.GetStreamsAsync(userIds: batch);
            var resultDict = response.Streams.ToDictionary(s => s.UserId, s => s);

            foreach (var channel in batch)
            {
                if (!_responseMap.Remove(channel, out var tcs)) continue;
                if (tcs.Task.IsCompleted)
                    continue;
                tcs.TrySetResult(resultDict.GetValueOrDefault(channel));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during batch fetch: {ex.Message}");
            foreach (var channel in batch)
            {
                if (!_responseMap.Remove(channel, out var tcs)) continue;
                if(!tcs.Task.IsCompleted)
                    tcs.TrySetException(ex);
            }
        }
    }
}