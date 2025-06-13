using System;
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
    private static readonly Lock Lock = new();
    private static readonly HashSet<string> PendingChannels = [];
    private static readonly Dictionary<string, TaskCompletionSource<Stream?>> ResponseMap = new();
    private static Timer _timer;
    private static readonly TwitchAPI _api = new();
    private readonly IConfiguration _configuration;

    public StreamInfoBatchService(IConfiguration configuration)
    {
        _timer = new Timer(_ => ProcessBatch(), null, Timeout.Infinite, Timeout.Infinite);
        _configuration = configuration;
        ConfigureTwitchApi();
    }
    
    private void ConfigureTwitchApi()
    {
        _api.Settings.ClientId = _configuration.GetValue<string>(Variables.TwitchClientId);
        _api.Settings.Secret = _configuration.GetValue<string>(Variables.TwitchClientSecret);
    }

    public Task<Stream?> RequestRawStreamAsync(string channelName)
    {
        lock (Lock)
        {
            if (!ResponseMap.ContainsKey(channelName))
            {
                PendingChannels.Add(channelName);
                ResponseMap[channelName] = new TaskCompletionSource<Stream?>();
            }

            var pendingCount = PendingChannels.Count;
            var delay = GetAdaptiveDelay(pendingCount);
            _timer.Change(delay, Timeout.Infinite); // debounce timer
            return ResponseMap[channelName].Task;
        }
    }
    
    private double _pendingEma;
    private const double EmaAlpha = 0.3; // smoothing factor (lower = smoother)
    private int GetAdaptiveDelay(int currentPendingCount)
    {
        // Update EMA
        _pendingEma = EmaAlpha * currentPendingCount + (1 - EmaAlpha) * _pendingEma;

        // Map EMA (1–20) to delay (800–100 ms)
        const double minDelay = 100;
        const double maxDelay = 800;
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

        lock (Lock)
        {
            batch = PendingChannels.Take(100).ToList();
            foreach (var name in batch)
                PendingChannels.Remove(name);
        }
        if (batch.Count == 0)
            return;
        Console.WriteLine($"[BatchService] Processing {batch.Count} channels: [{string.Join(", ", batch)}]");
        try
        {
            var response = await _api.Helix.Streams.GetStreamsAsync(userIds: batch);
            var resultDict = response.Streams.ToDictionary(s => s.UserId, s => s);

            foreach (var channel in batch)
            {
                if (!ResponseMap.Remove(channel, out var tcs)) continue;
                if (tcs.Task.IsCompleted)
                    continue;
                tcs.SetResult(resultDict.TryGetValue(channel, out var stream) ? stream : null);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during batch fetch: {ex.Message}");
            foreach (var channel in batch)
            {
                if (!ResponseMap.Remove(channel, out var tcs)) continue;
                if(!tcs.Task.IsCompleted)
                    tcs.SetException(ex);
            }
        }
    }
}