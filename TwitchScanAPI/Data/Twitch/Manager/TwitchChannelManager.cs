// TwitchChannelObserver.cs (Full Implementation)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using TwitchScanAPI.Global;
using TwitchScanAPI.Models;
using TwitchScanAPI.Models.Dto.Twitch.Channel;
using TwitchScanAPI.Services;

namespace TwitchScanAPI.Data.Twitch.Manager
{
    public class TwitchChannelManager : IDisposable
    {
        private readonly List<TwitchStatistics> _twitchStats = new();
        private readonly IConfiguration _configuration;
        private readonly TwitchAuthService _authService;
        private readonly NotificationService _notificationService;

        // Check every 30 minutes if the OAuth token needs to be refreshed
        private readonly Timer _oauthTimer = new(TimeSpan.FromMinutes(30).TotalMilliseconds);

        public TwitchChannelManager(IConfiguration configuration, TwitchAuthService authService, NotificationService notificationService)
        {
            _configuration = configuration;
            _authService = authService;
            _notificationService = notificationService;

            // Refresh the OAuth token on startup
            _ = RefreshAuthTokenAsync();

            // Initialize the timer to trigger token refresh every 30 minutes
            _oauthTimer.Elapsed += async (_, _) => await RefreshAuthTokenAsync();
            _oauthTimer.AutoReset = true;
            _oauthTimer.Start();
        }

        private async Task RefreshAuthTokenAsync()
        {
            try
            {
                // Update OAuth token
                var oauth = await _authService.GetOAuthTokenAsync();
                _configuration[Variables.TwitchOauthKey] = oauth;

                // Update OAuth token for all TwitchStatistics instances
                foreach (var channel in _twitchStats)
                {
                    await channel.RefreshConnectionAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing OAuth token: {ex.Message}");
            }
        }

        public Task<ResultMessage<string?>> Init(string channelName)
        {
            channelName = channelName.Trim();
            if (string.IsNullOrWhiteSpace(channelName) || channelName.Length < 2)
            {
                var error = new Error($"{channelName} is too short", StatusCodes.Status400BadRequest);
                return Task.FromResult(new ResultMessage<string?>(null, error));
            }

            if (_twitchStats.Any(x => string.Equals(x.ChannelName, channelName, StringComparison.OrdinalIgnoreCase)))
            {
                var error = new Error($"{channelName} already exists in Observer", StatusCodes.Status409Conflict);
                return Task.FromResult(new ResultMessage<string?>(null, error));
            }

            try
            {
                var stats = new TwitchStatistics(channelName, _configuration, _notificationService);
                _twitchStats.Add(stats);
            }
            catch (Exception e)
            {
                var error = new Error(e.Message, StatusCodes.Status403Forbidden);
                return Task.FromResult(new ResultMessage<string?>(null, error));
            }

            return Task.FromResult(new ResultMessage<string?>(channelName, null));
        }
        
        public async Task<ResultMessage<string?>> InitMultiple(string[] channelNames)
        {
            var results = new List<ResultMessage<string?>>();
            foreach (var channelName in channelNames)
            {
                var result = await Init(channelName);
                results.Add(result);
            }

            var errors = results.Where(x => x.Error != null).Select(x => x.Error!.ErrorMessage);
            var errorList = errors.ToList();
            var error = errorList.Any() ? new Error(string.Join(", ", errorList), StatusCodes.Status207MultiStatus) : null;
            return new ResultMessage<string?>(null, error);
        }

        public ResultMessage<string?> Remove(string channelName)
        {
            var channel = GetChannel(channelName);
            if (channel == null)
            {
                var error = new Error($"{channelName} not found", StatusCodes.Status404NotFound);
                return new ResultMessage<string?>(null, error);
            }

            channel.Dispose();
            _twitchStats.Remove(channel);
            return new ResultMessage<string?>(channelName, null);
        }

        public bool AddTextToObserve(string channelName, string text)
        {
            var channel = GetChannel(channelName);
            if (channel == null) return false;

            channel.AddTextToObserve(text);
            return true;
        }

        public IEnumerable<InitiatedChannel> GetInitiatedChannels()
        {
            return _twitchStats.Select(x => new InitiatedChannel(x.ChannelName, x.MessageCount, x.StartedAt, x.IsConnected));
        }

        public async Task<IEnumerable<string>> GetPossibleStatistics()
        {
            var stats = await GetAllStatistics();
            return _twitchStats.SelectMany(_ =>
            {
                var collection = stats.Keys;
                return collection;
            }).Distinct();
        }

        public async Task<IDictionary<string, IDictionary<string, object>?>> GetAllStatistics()
        {
            var stats = new Dictionary<string, IDictionary<string, object>?>();
            foreach (var channel in _twitchStats)
            {
                stats[channel.ChannelName] = await channel.GetStatisticsAsync();
            }

            return stats;
        }

        public async Task<IDictionary<string, object>?> GetAllStatistics(string channelName)
        {
            var stats = await GetChannel(channelName)?.GetStatisticsAsync()!;
            return stats;
        }

        public IEnumerable<string>? GetUsers(string channelName) => GetChannel(channelName)?.GetUsers();

        private TwitchStatistics? GetChannel(string channelName)
        {
            return _twitchStats.FirstOrDefault(x =>
                string.Equals(x.ChannelName, channelName, StringComparison.OrdinalIgnoreCase));
        }

        public void Dispose()
        {
            foreach (var channel in _twitchStats)
            {
                channel.Dispose();
            }

            _oauthTimer.Stop();
            _oauthTimer.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
