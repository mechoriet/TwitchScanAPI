using System;
using System.Collections.Generic;
using System.Linq;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;

namespace TwitchScanAPI.Data.Twitch.Manager;

public class TwitchPubSubManager : IDisposable
{
    private readonly List<TwitchPubSub> _pubSubClients = new();
    private readonly Dictionary<string, ChannelPubSubData> _channelSubscriptions = new();
    private readonly object _lock = new();
    private const int MaxClients = 10;
    private const int MaxTopicsPerClient = 50;

    public void SubscribeChannel(string channelId, string channelName)
    {
        lock (_lock)
        {
            if (_channelSubscriptions.ContainsKey(channelId))
                return;

            var clientCreation = GetOrCreateLeastLoadedClient();
            var channelData = new ChannelPubSubData(channelId, channelName);
            
            // Subscribe to topics for this channel
            clientCreation.client.ListenToVideoPlayback(channelId);
            
            // Track this subscription
            channelData.AssignedClient = clientCreation.client;
            _channelSubscriptions[channelId] = channelData;
            
            // If the client was already created previously, resend the topics
            if (clientCreation.existed)
            {
                clientCreation.client.SendTopics();
            }
        }
    }

    public void UnsubscribeChannel(string channelId)
    {
        lock (_lock)
        {
            if (!_channelSubscriptions.TryGetValue(channelId, out var channelData))
                return;

            var client = channelData.AssignedClient;
            _channelSubscriptions.Remove(channelId);
            
            if (client == null) return;
            if (GetClientTopicCount(client) != 0 || _pubSubClients.Count <= 1) return;
            client.Disconnect();
            _pubSubClients.Remove(client);
        }
    }

    private (TwitchPubSub client, bool existed) GetOrCreateLeastLoadedClient()
    {
        // If we have existing clients with capacity, use the one with the least topics
        var availableClients = _pubSubClients
            .Where(c => GetClientTopicCount(c) < MaxTopicsPerClient)
            .ToList();
            
        if (availableClients.Any())
        {
            return (availableClients.OrderBy(GetClientTopicCount).First(), true);
        }
        
        // If we need to create a new client and we're under the limit
        if (_pubSubClients.Count >= MaxClients) return (_pubSubClients.OrderBy(GetClientTopicCount).First(), true);
        var newClient = CreatePubSubClient();
        _pubSubClients.Add(newClient);
        return (newClient, false);

        // If we're at capacity, use the least loaded client
    }

    private int GetClientTopicCount(TwitchPubSub client)
    {
        return _channelSubscriptions.Count(s => s.Value.AssignedClient == client);
    }

    private TwitchPubSub CreatePubSubClient()
    {
        var client = new TwitchPubSub();
        
        // Set up the client's events
        client.OnViewCount += (_, args) => 
        {
            var channelId = args.ChannelId;
            if (!_channelSubscriptions.TryGetValue(channelId, out var channelData)) return;
            channelData.CurrentViewers = args.Viewers;
            OnViewCountChanged?.Invoke(this, new ViewCountChangedEventArgs(channelId, args.Viewers));
        };
        
        client.OnCommercial += (_, args) =>
        {
            OnCommercialStarted?.Invoke(this, args);
        };
        
        client.OnPubSubServiceConnected += (_, _) =>
        {
            client.SendTopics();
        };
        
        // Connect the client
        client.Connect();
        
        return client;
    }

    // Events for the TwitchClientManager to subscribe to
    public event EventHandler<ViewCountChangedEventArgs>? OnViewCountChanged;
    public event EventHandler<OnCommercialArgs>? OnCommercialStarted;
    
    public void Dispose()
    {
        foreach (var client in _pubSubClients)
        {
            client.Disconnect();
        }
        
        _pubSubClients.Clear();
        _channelSubscriptions.Clear();
    }
    
    // Helper class to store channel-specific data
    private class ChannelPubSubData(string channelId, string channelName)
    {
        public string ChannelId { get; } = channelId;
        public string ChannelName { get; } = channelName;
        public TwitchPubSub? AssignedClient { get; set; }
        public long? CurrentViewers { get; set; }
    }
    
    // Event args for view count changes
    public class ViewCountChangedEventArgs(string channelId, long viewers) : EventArgs
    {
        public string ChannelId { get; } = channelId;
        public long Viewers { get; } = viewers;
    }
}