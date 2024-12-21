using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Data.Statistics.Utilities;
using TwitchScanAPI.Models.Twitch.Chat;
using TwitchScanAPI.Models.Twitch.Statistics;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class BotLikelinessStatistic : IStatistic, IDisposable
    {
        // Snapshot management
        private const int SnapshotTopX = 100; // Number of top users to snapshot

        // Lock object for thread safety during cleanup
        private readonly object _cleanupLock = new();

        // Stores recent messages for similarity analysis
        private readonly ConcurrentQueue<MessageEntry> _recentMessages = new();
        private readonly TimeSpan _snapshotRetention = TimeSpan.FromMinutes(30); // Retain snapshots
        private readonly ConcurrentQueue<Snapshot> _snapshots = new();

        // Timer for periodic snapshots
        private readonly Timer _snapshotTimer;
        private readonly TimeSpan _timeWindow = TimeSpan.FromMinutes(10); // Time window for analysis

        // Stores metrics for each user
        private readonly ConcurrentDictionary<string, UserBotMetrics> _userMetrics = new();

        private readonly TimeSpan
            _userMetricTimeout = TimeSpan.FromMinutes(30); // Remove users with last message older than this

        public BotLikelinessStatistic()
        {
            // Initialize and start the snapshot timer
            _snapshotTimer = new Timer(TakeSnapshot, null, _timeWindow, _timeWindow);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _snapshotTimer?.Dispose();
        }

        public string Name => "BotLikeliness";

        public object GetResult()
        {
            // Retrieve top X users with highest bot-likeliness scores
            var topUsers = _userMetrics
                .Where(kvp => kvp.Value.TotalMessages > 10) // Filter out users with low message count
                .OrderByDescending(kvp => kvp.Value.BotScore)
                .Take(SnapshotTopX)
                .Select(kvp => new BotLikelinessResult
                {
                    Username = kvp.Key,
                    LikelinessPercentage = Math.Min(kvp.Value.BotScore, 100.0)
                })
                .ToList();

            return new BotResult
            {
                TopSuspiciousUsers = topUsers,
                RecentSnapshots = _snapshots.ToList()
            };
        }

        public Task Update(ChannelMessage message)
        {
            var username = message.ChatMessage.Username.ToLowerInvariant();
            var messageText = message.ChatMessage.Message.ToLowerInvariant().Trim();

            // Update user's individual metrics
            _userMetrics.AddOrUpdate(username,
                new UserBotMetrics(message),
                (_, metrics) => metrics.UpdateMetrics(message));

            // Enqueue the message for similarity analysis
            var messageEntry = new MessageEntry
            {
                Username = username,
                MessageText = messageText,
                Timestamp = message.Time
            };
            _recentMessages.Enqueue(messageEntry);

            // Clean up messages outside the time window
            CleanupOldMessages();

            // Analyze similarity with recent messages
            AnalyzeMessageSimilarity(messageEntry);

            return Task.CompletedTask;
        }

        private void CleanupOldMessages()
        {
            lock (_cleanupLock)
            {
                while (_recentMessages.TryPeek(out var oldestMessage))
                    if (DateTime.UtcNow - oldestMessage.Timestamp > _timeWindow)
                        _recentMessages.TryDequeue(out _);
                    else
                        break; // All remaining messages are within the time window
            }
        }

        private void CleanupOldMetrics()
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in _userMetrics.ToArray())
                if (now - kvp.Value.LastMessageTime > _userMetricTimeout)
                    _userMetrics.TryRemove(kvp.Key, out _);
        }

        private void AnalyzeMessageSimilarity(MessageEntry newMessage)
        {
            const double similarityThreshold = 0.9; // 90% similarity threshold
            const int ngramSize = 3; // Trigrams for N-gram analysis

            // Generate n-grams for the new message
            var newMessageNGrams = StatisticsUtils.GetNGrams(newMessage.MessageText, ngramSize);

            foreach (var existingMessage in _recentMessages.ToArray())
            {
                // Skip comparison with self and messages outside 10-second window
                if (existingMessage.Username == newMessage.Username) continue;
                if (Math.Abs((newMessage.Timestamp - existingMessage.Timestamp).TotalSeconds) > 10) continue;

                // Generate n-grams for the existing message
                var existingMessageNGrams = StatisticsUtils.GetNGrams(existingMessage.MessageText, ngramSize);

                // Calculate Jaccard similarity
                var similarity = StatisticsUtils.CalculateJaccardSimilarity(newMessageNGrams, existingMessageNGrams);

                if (!(similarity >= similarityThreshold)) continue;
                // Increase group behavior score for both users
                if (_userMetrics.TryGetValue(newMessage.Username, out var userMetrics))
                    userMetrics.IncreaseGroupBehaviorScore();

                if (_userMetrics.TryGetValue(existingMessage.Username, out var otherUserMetrics))
                    otherUserMetrics.IncreaseGroupBehaviorScore();
            }
        }

        private void TakeSnapshot(object? state)
        {
            // Retrieve top X users
            var topUsers = _userMetrics
                .OrderByDescending(kvp => kvp.Value.BotScore)
                .Take(SnapshotTopX)
                .Select(kvp => new SnapshotUser
                {
                    Username = kvp.Key,
                    LikelinessPercentage = Math.Min(kvp.Value.BotScore, 100.0)
                })
                .ToList();

            // Create a snapshot
            var snapshot = new Snapshot
            {
                Timestamp = DateTime.UtcNow,
                Users = topUsers
            };

            // Enqueue the snapshot
            _snapshots.Enqueue(snapshot);

            // Cleanup old snapshots
            CleanupOldSnapshots();
        }

        private void CleanupOldSnapshots()
        {
            while (_snapshots.TryPeek(out var oldestSnapshot))
                if (DateTime.UtcNow - oldestSnapshot.Timestamp > _snapshotRetention)
                    _snapshots.TryDequeue(out _);
                else
                    break; // All remaining snapshots are within the retention period
        }
    }
}