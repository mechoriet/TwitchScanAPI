#nullable enable

using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using TwitchScanAPI.Hubs;
using TwitchScanAPI.Models;
using TwitchScanAPI.Models.Dto.Twitch.Channel;

namespace TwitchScanAPI.Data
{
    public class TwitchChannelObserver
    {
        private readonly List<TwitchStatistics> _twitchStats = new();
        private readonly IHubContext<TwitchHub, ITwitchHub> _hubContext;
        private readonly IConfiguration _configuration;

        public TwitchChannelObserver(IHubContext<TwitchHub, ITwitchHub> hubContext, IConfiguration configuration)
        {
            _hubContext = hubContext;
            _configuration = configuration;
        }

        public ResultMessage<string?> Init(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName) || channelName.Length < 2)
            {
                var error = new Error($"{channelName} is too short", StatusCodes.Status400BadRequest);
                return new ResultMessage<string?>(null, error);
            }

            if (_twitchStats.Any(x => string.Equals(x.ChannelName, channelName, StringComparison.OrdinalIgnoreCase)))
            {
                var error = new Error($"{channelName} already exists in Observer", StatusCodes.Status409Conflict);
                return new ResultMessage<string?>(null, error);
            }
            
            var stats = new TwitchStatistics(channelName, _hubContext, _configuration);
            _twitchStats.Add(stats);

            return new ResultMessage<string?>(channelName, null);
        }
        
        public ResultMessage<string?> Remove(string channelName)
        {
            var channel = GetChannelStatistics(channelName);
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
            var channel = GetChannelStatistics(channelName);
            if (channel == null) return false;

            channel.AddTextToObserve(text);
            return true;
        }
        
        public IEnumerable<InitiatedChannel> GetInitiatedChannels()
        {
            return _twitchStats.Select(x => new InitiatedChannel
            {
                ChannelName = x.ChannelName,
                MessageCount = x.MessageCount
            });
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
                stats[channel.ChannelName] = await channel.GetStatistics();
            }

            return stats;
        }

        public async Task<IDictionary<string, object>?> GetAllStatistics(string channelName)
        {
            var stats = await GetChannelStatistics(channelName)?.GetStatistics()!;
            return stats;
        }

        public IEnumerable<string>? GetUsers(string channelName) => GetChannelStatistics(channelName)?.Users.Keys;

        private TwitchStatistics? GetChannelStatistics(string channelName)
        {
            return _twitchStats.FirstOrDefault(x =>
                string.Equals(x.ChannelName, channelName, StringComparison.OrdinalIgnoreCase));
        }
    }
}