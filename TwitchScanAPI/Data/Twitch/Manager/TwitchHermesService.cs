using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Prometheus;
using TwitchLib.PubSub.Events;
using TwitchScanAPI.Utilities;

namespace TwitchScanAPI.Data.Twitch.Manager;

public class TwitchHermesService
{
    private readonly ILogger _logger = LoggerFactoryHelper.CreateLogger<TwitchHermesService>();

    private readonly List<TwitchHermesClient> _hermesClients = [];
    private readonly Dictionary<string, HermesData> _channelSubscriptions = new(StringComparer.OrdinalIgnoreCase);
    private Lock _lock = new();
    private readonly int _maxClients = 200; //TODO: have to test what the limit is twitch enforces
    private readonly int _maxTopicsPerClient = 100; //TODO: test what the max topics is per Client
    private bool _disposed;

    // Prometheus metrics
    private static readonly Gauge ActivePubSubClients = Metrics.CreateGauge("twitch_pubsub_clients", "Number of active PubSub clients");
    private static readonly Counter ViewerCountUpdatesTotal = Metrics.CreateCounter("twitch_viewer_count_updates_total", "Total viewer count updates");
    private static readonly Counter CommercialEventsTotal = Metrics.CreateCounter("twitch_commercial_events_total", "Total commercial events");

    private async Task ReconnectAllClients()
    {
        if (_disposed) return;
        _logger.LogInformation("Reconnecting all Hermes clients.");
        foreach (var client in _hermesClients)
        {
            try
            {
                await client.DisconnectAsync();
                await client.ConnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reconnecting PubSub client.");
            }
        }
    }


    public void SubscribeChannel(string channelId, string channelName)
    {
        if (_disposed) return;
        if(string.IsNullOrWhiteSpace(channelId)) return;
        if(_channelSubscriptions.ContainsKey(channelId)) return;

        var clientCreation = GetOrCreateLeastLoadedClient();
        var channelData = new HermesData(channelId, channelName);
        try
        {
            _ = clientCreation.client.SubscribeToVideoPlayback(channelId);
            channelData.AssignedClient = clientCreation.client;
            _channelSubscriptions.Add(channelId, channelData);
            Console.WriteLine($"Subscribed to PubSub(hermes) topics for channel {channelName} ({channelId})");

            // Update metrics
            ActivePubSubClients.Set(_hermesClients.Count);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error subscribing to PubSub(hermes) topics for channel {channelName}: {ex.Message}");
        }
    }

    public void UnsubscribeChannel(string channelId)
    {
        if (_disposed) return;
        if (string.IsNullOrWhiteSpace(channelId)) return;
        var client = _channelSubscriptions[channelId];
        try
        {
            client.AssignedClient?.UnsubscribeFromVideoPlayback(channelId);
            _channelSubscriptions.Remove(channelId);

            // Update metrics
            ActivePubSubClients.Set(_hermesClients.Count);
        }
        catch (Exception err)
        {
            Console.WriteLine($"Error unsubscribing from PubSub client: {err.Message}");
        }
    }
    
    
    private (TwitchHermesClient client, bool existed) GetOrCreateLeastLoadedClient()
    {
        // If we have existing clients with capacity, use the one with the least topics
        var availableClients = _hermesClients
            .Where(c => GetClientTopicCount(c) < _maxTopicsPerClient)
            .ToList();

        if (availableClients.Any())
        {
            var leastLoadedClient = availableClients.OrderBy(GetClientTopicCount).First();
            return (leastLoadedClient, true);
        }

        // If we need to create a new client and we're under the limit
        if (_hermesClients.Count < _maxClients)
        {
            var newClient = CreatePubSubClient().Result;
            _hermesClients.Add(newClient);
            return (newClient, false);
        }

        // If we're at capacity, use the least loaded client
        return (_hermesClients.OrderBy(GetClientTopicCount).First(), true);
    }

    private int GetClientTopicCount(TwitchHermesClient client)
    {
        return _channelSubscriptions.Count(s => s.Value.AssignedClient == client);
    }
    
    private async Task<TwitchHermesClient> CreatePubSubClient()
    {
        var client = new TwitchHermesClient();
        client.OnViewCountReceived += (_,args) =>
        {
            if (_disposed) return;
            var channelId = args.ChannelId;
            if (!_channelSubscriptions.TryGetValue(channelId, out var channelData)) return;

            channelData.CurrentViewers = args.Viewers;
            OnViewCountChanged?.Invoke(this, new ViewCountChangedEventArgs(channelId, args.Viewers));
            ViewerCountUpdatesTotal.Inc();
        };
        client.OnCommercialReceived += (_, args) =>
        {
            if (_disposed) return;
            OnCommercialStarted?.Invoke(this,args);
            CommercialEventsTotal.Inc();
        };
        client.OnSubscriptionActiveChanged += (_, args) =>
        {
            if (_disposed) return;
            OnSubscriptionStateChange?.Invoke(this, args);
        };
        client.OnErrorOccurred += (_, args) =>
        {
            Console.WriteLine($"Error on hermes client: {args.Message} Stacktrace: {args.StackTrace}");

        };
        client.OnConnectionDead += (_, _) =>
        {
            HandleDeadClient(client);
        };
        try
        {
            await client.ConnectAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting new PubSub client: {ex.Message}");
        }
        return client;
    }

    private void HandleDeadClient(TwitchHermesClient deadClient)
    {
        lock (_lock)
        {
            if (_disposed) return;

            // Find all channels assigned to this dead client
            var affectedChannels = _channelSubscriptions
                .Where(kvp => kvp.Value.AssignedClient == deadClient)
                .Select(kvp => kvp.Key)
                .ToList();

            // Remove the dead client from the pool
            _hermesClients.Remove(deadClient);

            // Reassign each affected channel to a new client
            foreach (var channelId in affectedChannels)
            {
                var channelData = _channelSubscriptions[channelId];
                channelData.AssignedClient = null; // Clear the assignment

                // Get or create a new client for this channel
                var newClientResult = GetOrCreateLeastLoadedClient();
                var newClient = newClientResult.client;

                try
                {
                    _ = newClient.SubscribeToVideoPlayback(channelId);
                    channelData.AssignedClient = newClient;
                    Console.WriteLine($"Reassigned channel {channelData.ChannelName} ({channelId}) to a new client");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reassigning channel {channelData.ChannelName} ({channelId}): {ex.Message}");
                }
            }

            // Update metrics
            ActivePubSubClients.Set(_hermesClients.Count);
        }
    }
    
    
    
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        try
        {
            foreach (var client in _hermesClients)
            {
                _ = client.DisconnectAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disposing PubSubManager: {ex.Message}");
        }

        _hermesClients.Clear();
        _channelSubscriptions.Clear();

        GC.SuppressFinalize(this);
    }
    
    
    public event EventHandler<ViewCountChangedEventArgs>? OnViewCountChanged;
    public event EventHandler<OnCommercialArgs>? OnCommercialStarted;
    
    public event EventHandler<OnSubscriptionActive>? OnSubscriptionStateChange;
    
    // Helper class to store channel-specific data
    private class HermesData(string channelId, string channelName)
    {
        public string ChannelId { get; } = channelId;
        public string ChannelName { get; } = channelName;
        public TwitchHermesClient? AssignedClient { get; set; }
        public long? CurrentViewers { get; set; }
    }
    
    // Event args for view count changes
    public class ViewCountChangedEventArgs(string channelId, long viewers) : EventArgs
    {
        public string ChannelId { get; } = channelId;
        public long Viewers { get; } = viewers;
    }
}