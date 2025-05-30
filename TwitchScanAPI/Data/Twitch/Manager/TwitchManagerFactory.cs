﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace TwitchScanAPI.Data.Twitch.Manager;

public class TwitchManagerFactory(IConfiguration configuration)
{
    private readonly Dictionary<string, TwitchClientManager> _clientManagers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lockObject = new();
    private bool _disposed;

    public async Task<TwitchClientManager?> GetOrCreateClientManagerAsync(string channelName)
    {
        if (_disposed) return null;

        if (string.IsNullOrWhiteSpace(channelName))
            return null;

        channelName = channelName.ToLower().Trim();

        lock (_lockObject)
        {
            if (_clientManagers.TryGetValue(channelName, out var existingManager))
            {
                return existingManager;
            }
        }

        var newManager = await TwitchClientManager.CreateAsync(channelName, configuration);
        if (newManager == null) return newManager;
        lock (_lockObject)
        {
            _clientManagers[channelName] = newManager;
        }

        return newManager;
    }

    public void RemoveClientManager(string channelName)
    {
        if (_disposed) return;

        channelName = channelName.ToLower().Trim();

        lock (_lockObject)
        {
            if (!_clientManagers.TryGetValue(channelName, out var manager)) return;

            manager.Dispose();
            _clientManagers.Remove(channelName);
        }
    }
}
