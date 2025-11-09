using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TwitchScanAPI.Services;

namespace TwitchScanAPI.Data.Twitch.Manager;

public class TwitchManagerFactory(IConfiguration configuration)
{
    private readonly SharedTwitchClientManager _sharedTwitchClientManager = new();
    private readonly TwitchHermesService _hermesService = new();
    private readonly Dictionary<string, TwitchClientManager> _clientManagers = new(StringComparer.OrdinalIgnoreCase);
    private readonly StreamInfoBatchService _streamInfoBatchService = new(configuration);
    private readonly Lock _lockObject = new();

    public async Task<TwitchClientManager?> GetOrCreateClientManagerAsync(string channelName)
    {
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

        var newManager = await TwitchClientManager.CreateAsync(channelName, configuration, _sharedTwitchClientManager,_streamInfoBatchService, _hermesService);
        if (newManager == null) return newManager;
        lock (_lockObject)
        {
            _clientManagers[channelName] = newManager;
        }

        return newManager;
    }

    public void RemoveClientManager(string channelName)
    {
        channelName = channelName.ToLower().Trim();

        lock (_lockObject)
        {
            if (!_clientManagers.TryGetValue(channelName, out var manager)) return;

            manager.Dispose();
            _clientManagers.Remove(channelName);
        }
    }
}
