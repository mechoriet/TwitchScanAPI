using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.ML.SentimentAnalysis;
using TwitchScanAPI.Models.Twitch.Chat;
using TwitchScanAPI.Models.Twitch.Statistics;
using TwitchScanAPI.Services;
using VaderSharp2;
using UserSentiment = TwitchScanAPI.Models.Twitch.Statistics.UserSentiment;

namespace TwitchScanAPI.Data.Statistics.ML
{
    public class SentimentAnalysisStatistic : StatisticBase
    {
        private const int MinMessages = 5;
        private const int TopUsersCount = 10;
        private const int MaxTopMessages = 5; // Reduced from 10 to lower memory
        private static readonly SentimentIntensityAnalyzer Analyzer = new SentimentIntensityAnalyzer();

        private readonly TimeSpan _bucketSize = TimeSpan.FromMinutes(1);
        private ConcurrentDictionary<DateTime, SentimentScores> _sentimentOverTime = new();
        private List<SentimentMessage> _topNegativeMessages = new();
        private List<SentimentMessage> _topPositiveMessages = new();
        private readonly object _topNegativeMessagesLock = new();
        private readonly object _topPositiveMessagesLock = new();

        private ConcurrentDictionary<string, Models.ML.SentimentAnalysis.UserSentiment> _userSentiments =
            new(StringComparer.OrdinalIgnoreCase);

        public override string Name => "SentimentAnalysis";

        protected override object ComputeResult()
        {
            var sentimentData = _sentimentOverTime
                .Select(kv => new { kv.Key, Value = kv.Value.Compound });

            var trend = TrendService.CalculateTrend(
                sentimentData,
                d => d.Value,
                d => d.Key
            );

            return new SentimentAnalysisResult
            {
                SentimentOverTime = GetSentimentOverTime(),
                TopPositiveUsers = GetTopPositiveUsers(),
                TopNegativeUsers = GetTopNegativeUsers(),
                TopUsers = GetTopUsersWithSentiment(),
                TopPositiveMessages = GetTopPositiveMessages(),
                TopNegativeMessages = GetTopNegativeMessages(),
                Trend = trend
            };
        }

        public Task Update(ChannelMessage message)
        {
            var text = message.ChatMessage.Message;
            var username = message.ChatMessage.Username;

            // Skip sentiment analysis for very short messages or common commands to reduce CPU usage
            if (text.StartsWith('!') || text.StartsWith('/') || text.Contains("http"))
            {
                // Still update user sentiment with neutral score for short/common messages
                var fastexituserSentiment = _userSentiments.GetOrAdd(username.Trim(), u => new Models.ML.SentimentAnalysis.UserSentiment(u));
                lock (fastexituserSentiment.Lock)
                {
                    fastexituserSentiment.MessageCount++;
                    fastexituserSentiment.Neutral += 0.5; // Neutral score for skipped messages
                    fastexituserSentiment.LastUpdated = DateTime.UtcNow;
                }
                HasUpdated = true;
                return Task.CompletedTask;
            }

            // Analyze sentiment
            var results = Analyzer.PolarityScores(text);

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
            var userSentiment =
                _userSentiments.GetOrAdd(username.Trim(), u => new Models.ML.SentimentAnalysis.UserSentiment(u));
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
            HasUpdated = true;
            return Task.CompletedTask;
        }



        private IEnumerable<SentimentOverTime> GetSentimentOverTime()
        {
            return _sentimentOverTime
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => new SentimentOverTime
                {
                    Time = kvp.Key,
                    AveragePositive = kvp.Value.MessageCount > 0 ? Math.Round(kvp.Value.Positive / kvp.Value.MessageCount, 3) : 0,
                    AverageNegative = kvp.Value.MessageCount > 0 ? Math.Round(kvp.Value.Negative / kvp.Value.MessageCount, 3) : 0,
                    AverageNeutral = kvp.Value.MessageCount > 0 ? Math.Round(kvp.Value.Neutral / kvp.Value.MessageCount, 3) : 0,
                    AverageCompound = kvp.Value.MessageCount > 0 ? Math.Round(kvp.Value.Compound / kvp.Value.MessageCount, 3) : 0,
                    MessageCount = (int)kvp.Value.MessageCount
                });
        }

        private IEnumerable<UserSentiment> GetTopPositiveUsers()
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
                });
        }

        private IEnumerable<UserSentiment> GetTopNegativeUsers()
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
                });
        }

        private IEnumerable<UserSentiment> GetTopUsersWithSentiment()
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
                });
        }

        private IEnumerable<SentimentMessage> GetTopPositiveMessages()
        {
            lock (_topPositiveMessagesLock)
            {
                return _topPositiveMessages
                    .OrderByDescending(m => m.Compound)
                    .Take(MaxTopMessages);
            }
        }

        private IEnumerable<SentimentMessage> GetTopNegativeMessages()
        {
            lock (_topNegativeMessagesLock)
            {
                return _topNegativeMessages
                    .OrderBy(m => m.Compound)
                    .Take(MaxTopMessages);
            }
        }

        private void UpdateTopMessages(Models.ML.SentimentAnalysis.UserSentiment userSentiment, string message,
            SentimentAnalysisResults results, DateTime time)
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

            lock (_topPositiveMessagesLock)
            {
                if (results.Compound > 0)
                    AddTopMessage(_topPositiveMessages, sentimentMessage, (a, b) => b.Compound.CompareTo(a.Compound));
            }

            lock (_topNegativeMessagesLock)
            {
                if (results.Compound < 0)
                    AddTopMessage(_topNegativeMessages, sentimentMessage, (a, b) => a.Compound.CompareTo(b.Compound));
            }
        }

        private static void AddTopMessage(List<SentimentMessage> topMessages, SentimentMessage newMessage,
            Comparison<SentimentMessage> comparison)
        {
            var index = topMessages.BinarySearch(newMessage, Comparer<SentimentMessage>.Create(comparison));
            if (index < 0) index = ~index;
            topMessages.Insert(index, newMessage);
            if (topMessages.Count > MaxTopMessages)
                topMessages.RemoveAt(topMessages.Count - 1);
        }

        private DateTime GetBucketTime(DateTime time)
        {
            var bucketTicks = time.Ticks - time.Ticks % _bucketSize.Ticks;
            return new DateTime(bucketTicks, time.Kind);
        }

        public override void Dispose()
        {
            base.Dispose();
            _sentimentOverTime = new ConcurrentDictionary<DateTime, SentimentScores>();
            _userSentiments = new ConcurrentDictionary<string, Models.ML.SentimentAnalysis.UserSentiment>();
            _topPositiveMessages = new List<SentimentMessage>();
            _topNegativeMessages = new List<SentimentMessage>();
        }
    }
}
