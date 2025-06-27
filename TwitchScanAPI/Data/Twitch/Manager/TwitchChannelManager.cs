using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
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
        TwitchManagerFactory clientManagerFactory,
        NotificationService notificationService,
        MongoDbContext context)
        : IDisposable
    {
        private readonly Lock _lockObject = new();
        private bool _disposed;

        public readonly List<TwitchStatistics> TwitchStats = [];

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            foreach (var channel in TwitchStats) 
                channel.Dispose();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Initialize a channel to observe
        /// </summary>
        public async Task<ResultMessage<string?>> Init(string channelName)
        {
            if (_disposed) 
                return new ResultMessage<string?>(null, new Error("Service is shutting down", StatusCodes.Status503ServiceUnavailable));

            channelName = channelName.Trim();
            if (string.IsNullOrWhiteSpace(channelName) || channelName.Length < 2)
            {
                var error = new Error($"{channelName} is too short", StatusCodes.Status400BadRequest);
                return new ResultMessage<string?>(null, error);
            }

            lock (_lockObject)
            {
                if (TwitchStats.Any(x => string.Equals(x.ChannelName, channelName, StringComparison.OrdinalIgnoreCase)))
                {
                    var error = new Error($"{channelName} already exists in Observer", StatusCodes.Status409Conflict);
                    return new ResultMessage<string?>(null, error);
                }
            }

            try
            {
                var stats = await TwitchStatistics.CreateAsync(channelName, clientManagerFactory, notificationService,
                    context);
                if (stats == null)
                {
                    var error = new Error($"{channelName} not found", StatusCodes.Status404NotFound);
                    return new ResultMessage<string?>(null, error);
                }

                lock (_lockObject)
                {
                    TwitchStats.Add(stats);
                }

                Console.WriteLine($"Initialized channel: {channelName}");
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
            if (_disposed) 
                return new ResultMessage<string?>(null, new Error("Service is shutting down", StatusCodes.Status503ServiceUnavailable));

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
            if (_disposed) 
                return new ResultMessage<string?>(null, new Error("Service is shutting down", StatusCodes.Status503ServiceUnavailable));

            var channel = GetChannel(channelName);
            if (channel == null)
            {
                var error = new Error($"{channelName} not found", StatusCodes.Status404NotFound);
                return new ResultMessage<string?>(null, error);
            }

            channel.Dispose();

            lock (_lockObject)
            {
                TwitchStats.Remove(channel);
            }

            clientManagerFactory.RemoveClientManager(channelName);
            Console.WriteLine($"Removed channel: {channelName}");

            return new ResultMessage<string?>(channelName, null);
        }

        /// <summary>
        ///     Add a text to observe for a specific channel
        /// </summary>
        public bool AddTextToObserve(string channelName, string text)
        {
            if (_disposed) return false;
            if (string.IsNullOrWhiteSpace(text)) return false;

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
            if (_disposed) return [];

            var channels = new List<InitiatedChannel>();

            List<TwitchStatistics> statsSnapshot;
            lock (_lockObject)
            {
                statsSnapshot = TwitchStats.ToList();
            }

            foreach (var stat in statsSnapshot)
            {
                try
                {
                    var channelInfo = await stat.GetChannelInfoAsync();
                    channels.Add(new InitiatedChannel(stat.ChannelName, stat.MessageCount, stat.StartedAt,
                        channelInfo.Uptime,
                        channelInfo.IsOnline, channelInfo.Title, channelInfo.Game));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"TwitchChannelManager: Error getting channel info for {stat.ChannelName}: {ex.Message}");
                    channels.Add(new InitiatedChannel(stat.ChannelName, stat.MessageCount, stat.StartedAt,
                        DateTime.Now, false, "Error fetching info", ""));
                }
            }

            return channels;
        }

        /// <summary>
        ///     Get all possible statistics that can be observed
        /// </summary>
        public async Task<IEnumerable<string>> GetPossibleStatistics()
        {
            if (_disposed) return Array.Empty<string>();

            var stats = await GetAllStatistics();
            return stats.SelectMany(kvp => 
                kvp.Value?.Keys ?? Enumerable.Empty<string>()
            ).Distinct();
        }

        /// <summary>
        ///     Get all statistics for all channels
        /// </summary>
        public async Task<IDictionary<string, IDictionary<string, object>?>> GetAllStatistics()
        {
            if (_disposed) return new Dictionary<string, IDictionary<string, object>?>();

            var stats = new Dictionary<string, IDictionary<string, object>?>();

            List<TwitchStatistics> statsSnapshot;
            lock (_lockObject)
            {
                statsSnapshot = TwitchStats.ToList();
            }

            foreach (var channel in statsSnapshot)
            {
                try
                {
                    stats[channel.ChannelName] = await channel.GetStatisticsAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching statistics for {channel.ChannelName}: {ex.Message}");
                    stats[channel.ChannelName] = new Dictionary<string, object>();
                }
            }

            return stats;
        }

        /// <summary>
        ///     Get all statistics for a specific channel
        /// </summary>
        public async Task<IDictionary<string, object>?> GetAllStatistics(string channelName)
        {
            if (_disposed) return new Dictionary<string, object>();

            var channel = GetChannel(channelName);
            if (channel == null) return null;

            try
            {
                return await channel.GetStatisticsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching statistics for {channelName}: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        ///     Save snapshots current statistics to the database
        /// </summary>
        public async Task SaveSnapshotsAsync()
        {
            if (_disposed) return;

            List<TwitchStatistics> statsSnapshot;
            lock (_lockObject)
            {
                statsSnapshot = TwitchStats.ToList();
            }

            foreach (var channel in statsSnapshot.Where(channel => channel.IsOnline)) 
            {
                try
                {
                    await channel.SaveSnapshotAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving snapshot for {channel.ChannelName}: {ex.Message}");
                }
            }
        }

        /// <summary>
        ///    Save a snapshot of a specific channel to the database
        /// </summary>
        public async Task SaveSnapshotToChannelAsync(string channelName, StatisticsManager? manager = null,
            DateTime? date = null, int? viewCount = null)
        {
            if (_disposed) return;

            var channel = GetChannel(channelName);
            if (channel == null) return;

            try
            {
                await channel.SaveSnapshotAsync(manager, date, viewCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving snapshot for {channelName}: {ex.Message}");
            }
        }

        /// <summary>
        ///     Get the history keys and peak viewers
        /// </summary>
        public IEnumerable<StatisticTimeline> GetViewCountHistory(string channelName)
        {
            if (_disposed) return Array.Empty<StatisticTimeline>();

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
                Console.WriteLine($"Error getting view count history: {e.Message}");
                return Array.Empty<StatisticTimeline>();
            }
        }

        /// <summary>
        ///     Get the history of a specific key from the statistic history
        /// </summary>
        public StatisticHistory? GetHistoryByKey(string channelName, string id)
        {
            if (_disposed) return null;

            if (!Guid.TryParse(id, out var guidKey)) 
                throw new FormatException("Invalid GUID format");

            try
            {
                return context.StatisticHistory
                    .Find(Builders<StatisticHistory>.Filter.Eq(x => x.UserName, channelName) &
                          Builders<StatisticHistory>.Filter.Eq(x => x.Id, guidKey))
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting history by key: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     Get the chat history of a specific user in a channel using the "ChatHistory" statistic
        /// </summary>
        public IEnumerable<ChatHistory> GetChatHistory(string channelName, string username)
        {
            if (_disposed) return Array.Empty<ChatHistory>();
            if (string.IsNullOrWhiteSpace(username)) return Array.Empty<ChatHistory>();

            var channel = GetChannel(channelName);
            return channel == null ? Array.Empty<ChatHistory>() : channel.GetChatHistory(username);
        }

        /// <summary>
        ///     Get all users in a channel
        /// </summary>
        public IEnumerable<string>? GetUsers(string channelName)
        {
            if (_disposed) return null;

            return GetChannel(channelName)?.GetUsers();
        }

        /// <summary>
        ///     Check if all channels are offline
        /// </summary>
        public bool AllChannelsOffline()
        {
            if (_disposed) return true;

            lock (_lockObject)
            {
                return TwitchStats.All(x => !x.IsOnline);
            }
        }

        /// <summary>
        ///     Get the channel statistics
        /// </summary>
        private TwitchStatistics? GetChannel(string channelName)
        {
            if (_disposed || string.IsNullOrWhiteSpace(channelName)) return null;

            lock (_lockObject)
            {
                return TwitchStats.FirstOrDefault(x =>
                    string.Equals(x.ChannelName, channelName.Trim(), StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
