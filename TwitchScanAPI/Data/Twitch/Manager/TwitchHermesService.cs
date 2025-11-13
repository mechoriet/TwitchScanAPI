using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.PubSub.Events;
using TwitchScanAPI.Utilities;

namespace TwitchScanAPI.Data.Twitch.Manager;

public class TwitchHermesService
{

    private readonly List<TwitchHermesClient> _hermesClients = [];
    private readonly Dictionary<string, HermesData> _channelSubscriptions = new(StringComparer.OrdinalIgnoreCase);
    private Lock _lock = new();
    private int MaxClients = 200; //TODO: have to test what the limit is twitch enforces
    private int MaxTopicsPerClient = 100; //TODO: test what the max topics is per Client
    private bool _disposed;

    private async Task ReconnectAllClients()
    {
        if (_disposed) return;
        foreach (var client in _hermesClients)
        {
            try
            {
                await client.DisconnectAsync();
                await client.ConnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reconnecting PubSub client: {ex.Message}");
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error subscribing to PubSub(hermes) topics for channel {channelName}: {ex.Message}");
        }
    }

    public void UnsubscribeChannel(string channelId)
    {
        
        //TODO: Boilerplate
    }
    
    
    private (TwitchHermesClient client, bool existed) GetOrCreateLeastLoadedClient()
    {
        // If we have existing clients with capacity, use the one with the least topics
        var availableClients = _hermesClients
            .Where(c => GetClientTopicCount(c) < MaxTopicsPerClient)
            .ToList();

        if (availableClients.Any())
        {
            var leastLoadedClient = availableClients.OrderBy(GetClientTopicCount).First();
            return (leastLoadedClient, true);
        }

        // If we need to create a new client and we're under the limit
        if (_hermesClients.Count < MaxClients)
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
        };
        client.OnCommercialReceived += (_, args) =>
        {
            if (_disposed) return;
            OnCommercialStarted?.Invoke(this,args);
            
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
    
    public event EventHandler<onSubscriptionActive>? OnSubscriptionStateChange;
    
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