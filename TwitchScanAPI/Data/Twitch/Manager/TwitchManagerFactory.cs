using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace TwitchScanAPI.Data.Twitch.Manager;

public class TwitchManagerFactory(IConfiguration configuration) : IDisposable
{
    private readonly TwitchPubSubManager _pubSubManager = new();
    private readonly Dictionary<string, TwitchClientManager> _clientManagers = new();

    public async Task<TwitchClientManager?> GetOrCreateClientManagerAsync(string channelName)
    {
        if (_clientManagers.TryGetValue(channelName.ToLower(), out var existingManager))
        {
            return existingManager;
        }
        
        var newManager = await TwitchClientManager.CreateAsync(channelName, configuration, _pubSubManager);
        if (newManager != null)
        {
            _clientManagers[channelName.ToLower()] = newManager;
        }
        
        return newManager;
    }
    
    public void RemoveClientManager(string channelName)
    {
        if (!_clientManagers.TryGetValue(channelName.ToLower(), out var manager)) return;
        manager.Dispose();
        _clientManagers.Remove(channelName.ToLower());
    }
    
    public void Dispose()
    {
        foreach (var manager in _clientManagers.Values)
        {
            manager.Dispose();
        }
        
        _clientManagers.Clear();
        _pubSubManager.Dispose();
    }
}