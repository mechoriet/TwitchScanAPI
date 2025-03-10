// TwitchChannelObserver.cs (Full Implementation)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.DbContext;
using TwitchScanAPI.Models;
using TwitchScanAPI.Models.Twitch.Channel;
using TwitchScanAPI.Models.Twitch.Statistics;
using TwitchScanAPI.Services;

namespace TwitchScanAPI.Data.Twitch.Manager
{
    public class TwitchChannelManager(
        IConfiguration configuration,
        NotificationService notificationService,
        MongoDbContext context)
        : IDisposable
    {
        public readonly List<TwitchStatistics> TwitchStats = new();

        public void Dispose()
        {
            foreach (var channel in TwitchStats) channel.Dispose();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Initialize a channel to observe
        /// </summary>
        public async Task<ResultMessage<string?>> Init(string channelName)
        {
            channelName = channelName.Trim();
            if (string.IsNullOrWhiteSpace(channelName) || channelName.Length < 2)
            {
                var error = new Error($"{channelName} is too short", StatusCodes.Status400BadRequest);
                return new ResultMessage<string?>(null, error);
            }

            if (TwitchStats.Any(x => string.Equals(x.ChannelName, channelName, StringComparison.OrdinalIgnoreCase)))
            {
                var error = new Error($"{channelName} already exists in Observer", StatusCodes.Status409Conflict);
                return new ResultMessage<string?>(null, error);
            }

            try
            {
                var stats = await TwitchStatistics.CreateAsync(channelName, configuration, notificationService,
                    context);
                if (stats == null)
                {
                    var error = new Error($"{channelName} not found", StatusCodes.Status404NotFound);
                    return new ResultMessage<string?>(null, error);
                }

                TwitchStats.Add(stats);
            }
            catch (Exception e)
            {
                var error = new Error(e.Message, StatusCodes.Status403Forbidden);
                return new ResultMessage<string?>(null, error);
            }

            return new ResultMessage<string?>(channelName, null);
        }

        /// <summary>
        ///     Initialize multiple channels at once
        /// </summary>
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
            var error = errorList.Any()
                ? new Error(string.Join(", ", errorList), StatusCodes.Status207MultiStatus)
                : null;
            return new ResultMessage<string?>(null, error);
        }

        /// <summary>
        ///     Remove a channel from the observer
        /// </summary>
        public ResultMessage<string?> Remove(string channelName)
        {
            var channel = GetChannel(channelName);
            if (channel == null)
            {
                var error = new Error($"{channelName} not found", StatusCodes.Status404NotFound);
                return new ResultMessage<string?>(null, error);
            }

            channel.Dispose();
            TwitchStats.Remove(channel);
            return new ResultMessage<string?>(channelName, null);
        }

        /// <summary>
        ///     Add a text to observe for a specific channel
        /// </summary>
        public bool AddTextToObserve(string channelName, string text)
        {
            var channel = GetChannel(channelName);
            if (channel == null) return false;

            channel.AddTextToObserve(text);
            return true;
        }

        /// <summary>
        ///     Get all initiated channels
        /// </summary>
        public async Task<IEnumerable<InitiatedChannel>> GetInitiatedChannels()
        {
            var channels = new List<InitiatedChannel>();
            foreach (var stat in TwitchStats)
            {
                var channelInfo = await stat.GetChannelInfoAsync();
                channels.Add(new InitiatedChannel(stat.ChannelName, stat.MessageCount, stat.StartedAt,
                    channelInfo.Uptime,
                    channelInfo.IsOnline, channelInfo.Title, channelInfo.Game));
            }

            return channels;
        }

        /// <summary>
        ///     Get all possible statistics that can be observed
        /// </summary>
        public async Task<IEnumerable<string>> GetPossibleStatistics()
        {
            var stats = await GetAllStatistics();
            return TwitchStats.SelectMany(_ =>
            {
                var collection = stats.Keys;
                return collection;
            }).Distinct();
        }

        /// <summary>
        ///     Get all statistics for all channels
        /// </summary>
        public async Task<IDictionary<string, IDictionary<string, object>?>> GetAllStatistics()
        {
            var stats = new Dictionary<string, IDictionary<string, object>?>();
            foreach (var channel in TwitchStats) stats[channel.ChannelName] = await channel.GetStatisticsAsync();

            return stats;
        }

        /// <summary>
        ///     Get all statistics for a specific channel
        /// </summary>
        public async Task<IDictionary<string, object>?> GetAllStatistics(string channelName)
        {
            var stats = await GetChannel(channelName)?.GetStatisticsAsync()!;
            return stats;
        }

        /// <summary>
        ///     Save snapshots current statistics to the database
        /// </summary>
        public async Task SaveSnapshotsAsync()
        {
            foreach (var channel in TwitchStats.Where(channel => channel.IsOnline)) await channel.SaveSnapshotAsync();
        }

        /// <summary>
        ///    Save a snapshot of a specific channel to the database
        /// </summary>
        public async Task SaveSnapshotToChannelAsync(string channelName, StatisticsManager? manager = null,
            DateTime? date = null, int? viewCount = null)
        {
            var channel = GetChannel(channelName);
            if (channel == null) return;
            await channel.SaveSnapshotAsync(manager, date, viewCount);
        }

        /// <summary>
        ///     Get the history keys and peak viewers
        /// </summary>
        public IEnumerable<StatisticTimeline> GetViewCountHistory(string channelName)
        {
            try
            {
                return context.StatisticHistory
                    .Find(Builders<StatisticHistory>.Filter.Eq(x => x.UserName, channelName))
                    .ToList()
                    .Select(x => new StatisticTimeline
                    {
                        Id = x.Id.ToString(),
                        Time = x.Time,
                        PeakViewers = x.PeakViewers,
                        AverageViewers = x.AverageViewers
                    });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new List<StatisticTimeline>();
            }
        }

        /// <summary>
        ///     Get the history of a specific key from the statistic history
        /// </summary>
        public StatisticHistory GetHistoryByKey(string channelName, string id)
        {
            if (!Guid.TryParse(id, out var guidKey)) throw new FormatException("Invalid GUID format");

            return context.StatisticHistory
                .Find(Builders<StatisticHistory>.Filter.Eq(x => x.UserName, channelName) &
                      Builders<StatisticHistory>.Filter.Eq(x => x.Id, guidKey))
                .FirstOrDefault();
        }

        /// <summary>
        ///     Get the chat history of a specific user in a channel using the "ChatHistory" statistic
        /// </summary>
        public IEnumerable<ChatHistory> GetChatHistory(string channelName, string username)
        {
            var channel = GetChannel(channelName);
            return channel == null ? new List<ChatHistory>() : channel.GetChatHistory(username);
        }

        /// <summary>
        ///     Get all users in a channel
        /// </summary>
        public IEnumerable<string>? GetUsers(string channelName)
        {
            return GetChannel(channelName)?.GetUsers();
        }

        /// <summary>
        ///     Check if all channels are offline
        /// </summary>
        public bool AllChannelsOffline()
        {
            return TwitchStats.All(x => !x.IsOnline);
        }

        /// <summary>
        ///     Get the channel statistics
        /// </summary>
        private TwitchStatistics? GetChannel(string channelName)
        {
            return TwitchStats.FirstOrDefault(x =>
                string.Equals(x.ChannelName, channelName, StringComparison.OrdinalIgnoreCase));
        }
    }
}