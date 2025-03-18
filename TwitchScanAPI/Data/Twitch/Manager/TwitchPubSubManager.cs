using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;

namespace TwitchScanAPI.Data.Twitch.Manager;

public class TwitchPubSubManager : IDisposable
{
    private readonly List<TwitchPubSub> _pubSubClients = [];
    private readonly Dictionary<string, ChannelPubSubData> _channelSubscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();
    private const int MaxClients = 10;
    private const int MaxTopicsPerClient = 50;
    private readonly System.Timers.Timer _reconnectTimer;
    private readonly TimeSpan _reconnectInterval = TimeSpan.FromMinutes(5);
    private bool _disposed;

    public TwitchPubSubManager()
    {
        _reconnectTimer = new System.Timers.Timer(_reconnectInterval.TotalMilliseconds)
        {
            AutoReset = true
        };
        _reconnectTimer.Elapsed += (_, _) => ReconnectAllClients();
        _reconnectTimer.Start();
    }

    private void ReconnectAllClients()
    {
        if (_disposed) return;

        lock (_lock)
        {
            foreach (var client in _pubSubClients)
            {
                try
                {
                    client.Connect();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reconnecting PubSub client: {ex.Message}");
                }
            }
        }
    }

    public void SubscribeChannel(string channelId, string channelName)
    {
        if (_disposed) return;
        if (string.IsNullOrWhiteSpace(channelId)) return;

        lock (_lock)
        {
            if (_channelSubscriptions.ContainsKey(channelId))
                return;

            var clientCreation = GetOrCreateLeastLoadedClient();
            var channelData = new ChannelPubSubData(channelId, channelName);

            try
            {
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

                Console.WriteLine($"Subscribed to PubSub topics for channel {channelName} ({channelId})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error subscribing to PubSub topics for channel {channelName}: {ex.Message}");
            }
        }
    }

    public void UnsubscribeChannel(string channelId)
    {
        if (_disposed) return;
        if (string.IsNullOrWhiteSpace(channelId)) return;

        lock (_lock)
        {
            if (!_channelSubscriptions.TryGetValue(channelId, out var channelData))
                return;

            var client = channelData.AssignedClient;
            _channelSubscriptions.Remove(channelId);
            Console.WriteLine($"Unsubscribed from PubSub topics for channel {channelData.ChannelName}");

            if (client == null) return;

            // If this client has no more topics, and we have more than one client, dispose it
            if (GetClientTopicCount(client) != 0 || _pubSubClients.Count <= 1) return;

            client.Disconnect();
            _pubSubClients.Remove(client);
            Console.WriteLine("Removed unused PubSub client");
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
            var leastLoadedClient = availableClients.OrderBy(GetClientTopicCount).First();
            return (leastLoadedClient, true);
        }

        // If we need to create a new client and we're under the limit
        if (_pubSubClients.Count < MaxClients)
        {
            var newClient = CreatePubSubClient();
            _pubSubClients.Add(newClient);
            return (newClient, false);
        }

        // If we're at capacity, use the least loaded client
        return (_pubSubClients.OrderBy(GetClientTopicCount).First(), true);
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
            if (_disposed) return;

            var channelId = args.ChannelId;
            if (!_channelSubscriptions.TryGetValue(channelId, out var channelData)) return;

            channelData.CurrentViewers = args.Viewers;
            OnViewCountChanged?.Invoke(this, new ViewCountChangedEventArgs(channelId, args.Viewers));
        };

        client.OnStreamUp += (_, args) =>
        {
            if (_disposed) return;

            if (_channelSubscriptions.TryGetValue(args.ChannelId, out var channelData))
                Console.WriteLine($"PubSub: Stream UP for {channelData.ChannelName}");

            OnStreamUp?.Invoke(this, args);
        };

        client.OnStreamDown += (_, args) =>
        {
            if (_disposed) return;

            if (_channelSubscriptions.TryGetValue(args.ChannelId, out var channelData))
                Console.WriteLine($"PubSub: Stream DOWN for {channelData.ChannelName}");

            OnStreamDown?.Invoke(this, args);
        };

        client.OnCommercial += (_, args) =>
        {
            if (_disposed) return;
            OnCommercialStarted?.Invoke(this, args);
        };

        client.OnPubSubServiceConnected += (_, _) =>
        {
            if (_disposed) return;
            Console.WriteLine("PubSub service connected");
            client.SendTopics();
        };

        client.OnPubSubServiceError += (_, args) =>
        {
            if (_disposed) return;
            Console.WriteLine($"PubSub service error: {args.Exception.Message}");
        };

        client.OnPubSubServiceClosed += (_, _) =>
        {
            if (_disposed) return;
            Console.WriteLine("PubSub service closed. Attempting reconnect...");
            try
            {
                client.Connect();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reconnecting PubSub client: {ex.Message}");
            }
        };

        // Connect the client
        try
        {
            client.Connect();
            Console.WriteLine("Created and connected new PubSub client");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting new PubSub client: {ex.Message}");
        }

        return client;
    }

    // Events for the TwitchClientManager to subscribe to
    public event EventHandler<ViewCountChangedEventArgs>? OnViewCountChanged;
    public event EventHandler<OnCommercialArgs>? OnCommercialStarted;
    public event EventHandler<OnStreamUpArgs>? OnStreamUp;
    public event EventHandler<OnStreamDownArgs>? OnStreamDown;

    public void InvokeStreamUp(string channelId)
    {
        if (_disposed) return;
        if (string.IsNullOrWhiteSpace(channelId)) return;

        OnStreamUp?.Invoke(this, new OnStreamUpArgs { ChannelId = channelId });
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        try
        {
            _reconnectTimer.Stop();
            _reconnectTimer.Dispose();

            foreach (var client in _pubSubClients)
            {
                client.Disconnect();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disposing PubSubManager: {ex.Message}");
        }

        _pubSubClients.Clear();
        _channelSubscriptions.Clear();

        GC.SuppressFinalize(this);
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
