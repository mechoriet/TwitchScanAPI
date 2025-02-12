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
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly Stopwatch _appRunTime = new();
        private Task? _backgroundTask;
        private readonly CancellationTokenSource _cts = new();

        public RestartHostedService(TwitchChannelManager twitchChannelManager,
            IHostApplicationLifetime hostApplicationLifetime)
        {
            _twitchChannelManager = twitchChannelManager;
            _hostApplicationLifetime = hostApplicationLifetime;
            _appRunTime.Start();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _backgroundTask = Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5), _cts.Token);

                        var allOffline = _twitchChannelManager.AllChannelsOffline();
                        if (!allOffline || !(_appRunTime.Elapsed.TotalHours > 24)) continue;
                        _hostApplicationLifetime.StopApplication();
                        break;
                    }
                    catch (TaskCanceledException)
                    {
                        // Swallow expected cancellation exception
                    }
                }
            }, _cts.Token);

            return Task.CompletedTask; // Allow the application to continue starting
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel(); // Signal the background task to stop
            return _backgroundTask ?? Task.CompletedTask;
        }
    }
}