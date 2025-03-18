using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using TwitchScanAPI.Data.Twitch.Manager;
using TwitchScanAPI.DbContext;
using TwitchScanAPI.Global;
using TwitchScanAPI.Models.Twitch.Statistics;
using TwitchScanAPI.Services;
using Timer = System.Timers.Timer;

namespace TwitchScanAPI.HostedServices
{
    public class TwitchChannelManagerHostedService : IHostedService, IDisposable
    {
        private readonly TwitchAuthService _authService;
        private readonly TwitchChannelManager _channelManager;
        private readonly IConfiguration _configuration;
        private readonly MongoDbContext _context;
        private readonly Timer _oauthTimer;
        private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(30);

        public TwitchChannelManagerHostedService(TwitchAuthService authService, MongoDbContext context,
            IConfiguration configuration, TwitchChannelManager channelManager)
        {
            // Initialize the timer to trigger token refresh every 30 minutes
            _authService = authService;
            _context = context;
            _configuration = configuration;
            _channelManager = channelManager;
            _oauthTimer = new Timer(_refreshInterval.TotalMilliseconds);
            _oauthTimer.Elapsed += async (_, _) => await RefreshAuthTokenAsync();
            _oauthTimer.Start();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _oauthTimer.Stop();
            _oauthTimer.Dispose();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Refresh the OAuth token on startup
            await RefreshAuthTokenAsync();

            // Initialize the observer from the database
            await InitiateFromDbAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return Task.CompletedTask;
        }

        private async Task RefreshAuthTokenAsync()
        {
            try
            {
                // Update OAuth token
                var oauth = await _authService.GetOAuthTokenAsync();
                if (string.IsNullOrEmpty(oauth)) throw new Exception("OAuth token is empty");
                _configuration[Variables.TwitchOauthKey] = oauth;
                Console.WriteLine($"Refreshed OAuth token successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing OAuth token: {ex.Message}");
            }
        }

        private async Task InitiateFromDbAsync()
        {
            var channels = await _context.StatisticHistory
                .Distinct(x => x.UserName, Builders<StatisticHistory>.Filter.Empty)
                .ToListAsync();

            foreach (var channel in channels) await _channelManager.Init(channel);
        }
    }
}