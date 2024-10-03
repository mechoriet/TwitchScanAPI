using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Dto.Statistics;
using TwitchScanAPI.Models.ML.SentimentAnalysis;
using TwitchScanAPI.Models.Twitch.Chat;
using VaderSharp;

namespace TwitchScanAPI.Data.Statistics.ML
{
    public class SentimentAnalysisStatistic : IStatistic
    {
        private const int MinMessages = 5;
        private const int TopUsersCount = 10;
        private const int MaxTopMessages = 10;

        public string Name => "SentimentAnalysis";

        private readonly SentimentIntensityAnalyzer _analyzer = new();

        // Store aggregated sentiment scores over time
        private readonly ConcurrentDictionary<DateTime, SentimentScores> _sentimentOverTime = new();

        // Track per-user sentiment
        private readonly ConcurrentDictionary<string, UserSentiment> _userSentiments =
            new(StringComparer.OrdinalIgnoreCase);

        // Define the time interval for bucketing (e.g., 5 minutes)
        private readonly TimeSpan _bucketSize = TimeSpan.FromMinutes(1);
        // Define the retention period for the cleanup (e.g., keep data for 24 hours)
        private readonly TimeSpan _retentionPeriod = TimeSpan.FromHours(24);
        // Define the cleanup interval for the timer (e.g., every hour)
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);

        // Collections to store top messages
        private readonly List<SentimentMessageDTO> _topPositiveMessages = new();
        private readonly List<SentimentMessageDTO> _topNegativeMessages = new();

        // Locks for thread-safe operations on message lists
        private readonly object _topPositiveMessagesLock = new();
        private readonly object _topNegativeMessagesLock = new();
        
        // Timer for periodic cleanup
        private readonly Timer _cleanupTimer;
        
        public SentimentAnalysisStatistic()
        {
            // Initialize the timer to trigger cleanup
            _cleanupTimer = new Timer(_cleanupInterval.TotalMilliseconds);
            _cleanupTimer.Elapsed += (_, _) => CleanupOldData();
            _cleanupTimer.AutoReset = true;  // Ensures the timer will keep triggering every hour
            _cleanupTimer.Start();
        }

        public object GetResult()
        {
            var result = new SentimentAnalysisResultDTO
            {
                SentimentOverTime = GetSentimentOverTime(),
                TopPositiveUsers = GetTopPositiveUsers(),
                TopNegativeUsers = GetTopNegativeUsers(),
                TopPositiveMessages = GetTopPositiveMessages(),
                TopNegativeMessages = GetTopNegativeMessages()
            };

            return result;
        }

        public void Update(ChannelMessage message)
        {
            var text = message.ChatMessage.Message;
            var username = message.ChatMessage.Username;
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(username))
                return;

            // Analyze sentiment
            var results = _analyzer.PolarityScores(text);

            // Update time-based sentiment scores
            var bucketTime = GetBucketTime(message.Time);
            var sentimentScores = _sentimentOverTime.GetOrAdd(bucketTime, _ => new SentimentScores());

            lock (sentimentScores.Lock)
            {
                sentimentScores.MessageCount++;
                sentimentScores.Positive += results.Positive;
                sentimentScores.Negative += results.Negative;
                sentimentScores.Neutral += results.Neutral;
                sentimentScores.Compound += results.Compound;
            }

            // Update per-user sentiment scores
            var userSentiment = _userSentiments.GetOrAdd(username.Trim(), u => new UserSentiment(u));

            lock (userSentiment.Lock)
            {
                userSentiment.MessageCount++;
                userSentiment.Positive += results.Positive;
                userSentiment.Negative += results.Negative;
                userSentiment.Neutral += results.Neutral;
                userSentiment.Compound += results.Compound;
                userSentiment.LastUpdated = DateTime.UtcNow;
            }

            // Update top positive messages
            var positiveMessage = new SentimentMessageDTO
            {
                Username = username.Trim(),
                Message = text,
                Positive = results.Positive,
                Negative = results.Negative,
                Neutral = results.Neutral,
                Compound = results.Compound,
                Time = message.Time
            };
            lock (_topPositiveMessagesLock)
            {
                AddTopMessage(_topPositiveMessages, positiveMessage, (a, b) => b.Compound.CompareTo(a.Compound));
            }

            // Update top negative messages
            var negativeMessage = new SentimentMessageDTO
            {
                Username = username.Trim(),
                Message = text,
                Positive = results.Positive,
                Negative = results.Negative,
                Neutral = results.Neutral,
                Compound = results.Compound,
                Time = message.Time
            };
            lock (_topNegativeMessagesLock)
            {
                AddTopMessage(_topNegativeMessages, negativeMessage, (a, b) => a.Compound.CompareTo(b.Compound));
            }
        }
        
        private void CleanupOldData()
        {
            var expirationTime = DateTime.UtcNow - _retentionPeriod;

            // Clean up old sentiment scores based on time
            foreach (var key in _sentimentOverTime.Keys)
            {
                if (key < expirationTime)
                {
                    _sentimentOverTime.TryRemove(key, out _);
                }
            }

            // Clean up old user sentiments
            foreach (var key in _userSentiments.Keys)
            {
                var userSentiment = _userSentiments[key];
                if (userSentiment.LastUpdated < expirationTime)
                {
                    _userSentiments.TryRemove(key, out _);
                }
            }
        }

        private DateTime GetBucketTime(DateTime time)
        {
            // Align time to the bucket interval
            var bucketTicks = time.Ticks - (time.Ticks % _bucketSize.Ticks);
            return new DateTime(bucketTicks, time.Kind);
        }

        private List<SentimentOverTimeDTO> GetSentimentOverTime()
        {
            return _sentimentOverTime
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => new SentimentOverTimeDTO
                {
                    Time = kvp.Key,
                    AveragePositive = kvp.Value.MessageCount > 0 ? kvp.Value.Positive / kvp.Value.MessageCount : 0,
                    AverageNegative = kvp.Value.MessageCount > 0 ? kvp.Value.Negative / kvp.Value.MessageCount : 0,
                    AverageNeutral = kvp.Value.MessageCount > 0 ? kvp.Value.Neutral / kvp.Value.MessageCount : 0,
                    AverageCompound = kvp.Value.MessageCount > 0 ? kvp.Value.Compound / kvp.Value.MessageCount : 0,
                    MessageCount = (int)kvp.Value.MessageCount
                })
                .ToList();
        }

        private List<UserSentimentDTO> GetTopPositiveUsers()
        {
            return _userSentiments.Values
                .Where(u => u.MessageCount >= MinMessages)
                .OrderByDescending(u => u.AverageCompound)
                .Take(TopUsersCount)
                .Select(u => new UserSentimentDTO
                {
                    Username = u.Username,
                    AveragePositive = Math.Round(u.AveragePositive, 3),
                    AverageNegative = Math.Round(u.AverageNegative, 3),
                    AverageNeutral = Math.Round(u.AverageNeutral, 3),
                    AverageCompound = Math.Round(u.AverageCompound, 3),
                    MessageCount = (int)u.MessageCount
                })
                .ToList();
        }

        private List<UserSentimentDTO> GetTopNegativeUsers()
        {
            return _userSentiments.Values
                .Where(u => u.MessageCount >= MinMessages)
                .OrderBy(u => u.AverageCompound)
                .Take(TopUsersCount)
                .Select(u => new UserSentimentDTO
                {
                    Username = u.Username,
                    AveragePositive = Math.Round(u.AveragePositive, 3),
                    AverageNegative = Math.Round(u.AverageNegative, 3),
                    AverageNeutral = Math.Round(u.AverageNeutral, 3),
                    AverageCompound = Math.Round(u.AverageCompound, 3),
                    MessageCount = (int)u.MessageCount
                })
                .ToList();
        }

        private List<SentimentMessageDTO> GetTopPositiveMessages()
        {
            lock (_topPositiveMessagesLock)
            {
                return _topPositiveMessages
                    .OrderByDescending(m => m.Compound)
                    .Take(MaxTopMessages)
                    .ToList();
            }
        }

        private List<SentimentMessageDTO> GetTopNegativeMessages()
        {
            lock (_topNegativeMessagesLock)
            {
                return _topNegativeMessages
                    .OrderBy(m => m.Compound)
                    .Take(MaxTopMessages)
                    .ToList();
            }
        }

        private void AddTopMessage(List<SentimentMessageDTO> topMessages, SentimentMessageDTO newMessage,
            Comparison<SentimentMessageDTO> comparison)
        {
            lock (topMessages == _topPositiveMessages ? _topPositiveMessagesLock : _topNegativeMessagesLock)
            {
                // Binary search to find the correct insertion point
                var index = topMessages.BinarySearch(newMessage, Comparer<SentimentMessageDTO>.Create(comparison));
                if (index < 0)
                {
                    index = ~index;
                }

                topMessages.Insert(index, newMessage);

                // If the list exceeds the maximum size, remove the last item
                if (topMessages.Count > MaxTopMessages)
                {
                    topMessages.RemoveAt(topMessages.Count - 1);
                }
            }
        }
    }
}