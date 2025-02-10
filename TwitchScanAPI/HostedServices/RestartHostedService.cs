using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using TwitchScanAPI.Data.Twitch.Manager;

namespace TwitchScanAPI.HostedServices
{
    // Once every 5 minutes, check if all channels are offline and the last restart was at least 24 hours ago. If so restart the service.
    public class RestartHostedService : IHostedService
    {
        private readonly TwitchChannelManager _twitchChannelManager;
        private readonly Stopwatch _appRunTime = new();
        
        public RestartHostedService(TwitchChannelManager twitchChannelManager)
        {
            _twitchChannelManager = twitchChannelManager;
            _appRunTime.Start();
        }
        
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                var allOffline = _twitchChannelManager.AllChannelsOffline();
                if (allOffline && _appRunTime.Elapsed.TotalHours > 24)
                {
                    Environment.Exit(0);
                }
            };
        }
        
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}