using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.ML.SentimentAnalysis;
using TwitchScanAPI.Models.Twitch.Chat;
using TwitchScanAPI.Models.Twitch.Statistics;
using TwitchScanAPI.Services;
using VaderSharp;
using UserSentiment = TwitchScanAPI.Models.Twitch.Statistics.UserSentiment;

namespace TwitchScanAPI.Data.Statistics.ML
{
    public class SentimentAnalysisStatistic : IStatistic
    {
        private const int MinMessages = 5;
        private const int TopUsersCount = 10;
        private const int MaxTopMessages = 10;

        public string Name => "SentimentAnalysis";

        private readonly SentimentIntensityAnalyzer _analyzer = new();
        private readonly ConcurrentDictionary<DateTime, SentimentScores> _sentimentOverTime = new();
        private readonly ConcurrentDictionary<string, Models.ML.SentimentAnalysis.UserSentiment> _userSentiments = new(StringComparer.OrdinalIgnoreCase);

        private readonly TimeSpan _bucketSize = TimeSpan.FromMinutes(1);
        private readonly List<SentimentMessage> _topPositiveMessages = new();
        private readonly List<SentimentMessage> _topNegativeMessages = new();
        private readonly object _topPositiveMessagesLock = new();
        private readonly object _topNegativeMessagesLock = new();

        public object GetResult()
        {
            var sentimentData = _sentimentOverTime
                .Select(kv => new { Key = kv.Key, Value = kv.Value.Compound })
                .ToList();
            
            var trend = TrendService.CalculateTrend(
                sentimentData,
                d => d.Value,
                d => d.Key
            );

            var result = new SentimentAnalysisResult
            {
                SentimentOverTime = GetSentimentOverTime(),
                TopPositiveUsers = GetTopPositiveUsers(),
                TopNegativeUsers = GetTopNegativeUsers(),
                TopUsers = GetTopUsersWithSentiment(),
                TopPositiveMessages = GetTopPositiveMessages(),
                TopNegativeMessages = GetTopNegativeMessages(),
                Trend = trend
            };

            return result;
        }

        public Task Update(ChannelMessage message)
        {
            var text = message.ChatMessage.Message;
            var username = message.ChatMessage.Username;

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
            var userSentiment = _userSentiments.GetOrAdd(username.Trim(), u => new Models.ML.SentimentAnalysis.UserSentiment(u));

            lock (userSentiment.Lock)
            {
                userSentiment.MessageCount++;
                userSentiment.Positive += results.Positive;
                userSentiment.Negative += results.Negative;
                userSentiment.Neutral += results.Neutral;
                userSentiment.Compound += results.Compound;
                userSentiment.LastUpdated = DateTime.UtcNow;
            }

            // Update top positive and negative messages
            UpdateTopMessages(userSentiment, text, results, message.Time);

            return Task.CompletedTask;
        }

        private List<SentimentOverTime> GetSentimentOverTime()
        {
            return _sentimentOverTime
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => new SentimentOverTime
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

        private List<UserSentiment> GetTopPositiveUsers()
        {
            return _userSentiments.Values
                .Where(u => u.MessageCount >= MinMessages)
                .OrderByDescending(u => u.AverageCompound)
                .Take(TopUsersCount)
                .Select(u => new UserSentiment
                {
                    Username = u.Username,
                    AveragePositive = Math.Round(u.AveragePositive, 3),
                    AverageNegative = Math.Round(u.AverageNegative, 3),
                    AverageNeutral = Math.Round(u.AverageNeutral, 3),
                    AverageCompound = Math.Round(u.AverageCompound, 3),
                    MessageCount = u.MessageCount
                })
                .ToList();
        }

        private List<UserSentiment> GetTopNegativeUsers()
        {
            return _userSentiments.Values
                .Where(u => u.MessageCount >= MinMessages)
                .OrderBy(u => u.AverageCompound)
                .Take(TopUsersCount)
                .Select(u => new UserSentiment
                {
                    Username = u.Username,
                    AveragePositive = Math.Round(u.AveragePositive, 3),
                    AverageNegative = Math.Round(u.AverageNegative, 3),
                    AverageNeutral = Math.Round(u.AverageNeutral, 3),
                    AverageCompound = Math.Round(u.AverageCompound, 3),
                    MessageCount = u.MessageCount
                })
                .ToList();
        }

        private List<UserSentiment> GetTopUsersWithSentiment()
        {
            return _userSentiments.Values
                .OrderByDescending(u => u.MessageCount)
                .Take(TopUsersCount)
                .Select(u => new UserSentiment
                {
                    Username = u.Username,
                    AveragePositive = Math.Round(u.AveragePositive, 3),
                    AverageNegative = Math.Round(u.AverageNegative, 3),
                    AverageNeutral = Math.Round(u.AverageNeutral, 3),
                    AverageCompound = Math.Round(u.AverageCompound, 3),
                    MessageCount = u.MessageCount
                })
                .ToList();
        }

        private List<SentimentMessage> GetTopPositiveMessages()
        {
            lock (_topPositiveMessagesLock)
            {
                return _topPositiveMessages
                    .OrderByDescending(m => m.Compound)
                    .Take(MaxTopMessages)
                    .ToList();
            }
        }

        private List<SentimentMessage> GetTopNegativeMessages()
        {
            lock (_topNegativeMessagesLock)
            {
                return _topNegativeMessages
                    .OrderBy(m => m.Compound)
                    .Take(MaxTopMessages)
                    .ToList();
            }
        }

        private void UpdateTopMessages(Models.ML.SentimentAnalysis.UserSentiment userSentiment, string message, SentimentAnalysisResults results, DateTime time)
        {
            var sentimentMessage = new SentimentMessage
            {
                Username = userSentiment.Username,
                Message = message,
                Positive = results.Positive,
                Negative = results.Negative,
                Neutral = results.Neutral,
                Compound = results.Compound,
                Time = time
            };

            // Update top positive messages
            lock (_topPositiveMessagesLock)
            {
                if (results.Compound > 0)
                {
                    AddTopMessage(_topPositiveMessages, sentimentMessage, (a, b) => b.Compound.CompareTo(a.Compound));
                }
            }

            // Update top negative messages
            lock (_topNegativeMessagesLock)
            {
                if (results.Compound < 0)
                {
                    AddTopMessage(_topNegativeMessages, sentimentMessage, (a, b) => a.Compound.CompareTo(b.Compound));
                }
            }
        }

        private void AddTopMessage(List<SentimentMessage> topMessages, SentimentMessage newMessage, Comparison<SentimentMessage> comparison)
        {
            var index = topMessages.BinarySearch(newMessage, Comparer<SentimentMessage>.Create(comparison));
            if (index < 0)
            {
                index = ~index;
            }

            topMessages.Insert(index, newMessage);

            if (topMessages.Count > MaxTopMessages)
            {
                topMessages.RemoveAt(topMessages.Count - 1);
            }
        }

        private DateTime GetBucketTime(DateTime time)
        {
            var bucketTicks = time.Ticks - (time.Ticks % _bucketSize.Ticks);
            return new DateTime(bucketTicks, time.Kind);
        }
    }
}