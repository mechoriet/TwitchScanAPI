using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Prometheus;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;
using TwitchScanAPI.Global;

namespace TwitchScanAPI.Services;

public class StreamInfoBatchService
{
    private static readonly Counter StreamInfoRequestsTotal = Metrics.CreateCounter("stream_twitchapi_batch_requests_total", "Total number of stream info requests");
    private static readonly Counter StreamInfoBatchesProcessedTotal = Metrics.CreateCounter("stream_twitchapi_batch_batches_processed_total", "Total batches processed", "status");
    private static readonly Histogram StreamInfoBatchProcessingDuration = Metrics.CreateHistogram("stream_twitchapi_batch_processing_duration_seconds", "Time taken to process a batch");
    private static readonly Gauge StreamInfoPendingChannels = Metrics.CreateGauge("stream_twitchapi_batch_pending_channels", "Number of pending channels");
    private static readonly Histogram StreamTwitchApiBatchSize = Metrics.CreateHistogram(
        name: "stream_twitchapi_batch_size",
        help: "Size of batch sent to Twitch API",
        new HistogramConfiguration()
        {
            Buckets = Histogram.LinearBuckets(start: 0, width: 5, count: 21) // 0–100 in steps of 5
        }
    );

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

        // Start a timer to update pending channels gauge every second
        var gaugeUpdateTimer = new Timer(_ => StreamInfoPendingChannels.Set(PendingChannels.Count), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
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
            StreamInfoRequestsTotal.Inc();
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

        // Map EMA (1–20) to delay (200–600 ms)
        const double minDelay = 200;
        const double maxDelay = 600;
        var clampedEma = Math.Clamp(_pendingEma, 1, 20);

        // Invert: more requests = shorter delay
        var scale = (clampedEma - 1) / (20 - 1); // range: 0–1
        var delay = (int)(maxDelay - scale * (maxDelay - minDelay));
        /*Console.WriteLine($"[Debounce] Pending={currentPendingCount}, EMA={_pendingEma:F2}, Delay={delay}ms");*/
        return delay;
    }
    private async void ProcessBatch()
    {
        using (StreamInfoBatchProcessingDuration.NewTimer())
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
            /*Console.WriteLine($"[BatchService] Processing {batch.Count}");*/
            try
            {
                StreamTwitchApiBatchSize.Observe(batch.Count);
                var response = await Api.Helix.Streams.GetStreamsAsync(userIds: batch, first: 100);
                var resultDict = response.Streams.ToDictionary(s => s.UserId, s => s);

                foreach (var channel in batch)
                {
                    if (!_responseMap.Remove(channel, out var tcs)) continue;
                    if (tcs.Task.IsCompleted)
                        continue;
                    tcs.TrySetResult(resultDict.GetValueOrDefault(channel));
                }
                StreamInfoBatchesProcessedTotal.WithLabels("success").Inc();
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
                StreamInfoBatchesProcessedTotal.WithLabels("failure").Inc();
            }
        }
    }
}