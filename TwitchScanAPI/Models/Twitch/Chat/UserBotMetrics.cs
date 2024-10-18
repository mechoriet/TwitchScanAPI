using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace TwitchScanAPI.Models.Twitch.Chat
{
    // Class to store metrics for a single user
     public class UserBotMetrics
    {
        public long TotalMessages;
        private long _totalLength;
        private readonly ConcurrentDictionary<string, int> _messageCounts = new();
        private readonly ConcurrentQueue<DateTime> _messageTimes = new();

        private readonly TimeSpan _frequencyTimeWindow = TimeSpan.FromMinutes(1); // Time window for frequency analysis
        private readonly int _maxStoredMessages = 100; // Limit for stored message times

        private double _groupBehaviorScore;

        public double BotScore { get; private set; }
        public DateTime LastMessageTime => _messageTimes.LastOrDefault();

        public UserBotMetrics(ChannelMessage initialMessage)
        {
            TotalMessages = 1;
            _totalLength = initialMessage.ChatMessage.Message.Length;

            _messageCounts.TryAdd(initialMessage.ChatMessage.Message, 1);
            _messageTimes.Enqueue(initialMessage.Time);

            CalculateBotScore();
        }

        public UserBotMetrics UpdateMetrics(ChannelMessage message)
        {
            TotalMessages++;
            _totalLength += message.ChatMessage.Message.Length;

            // Update repetition counts
            _messageCounts.AddOrUpdate(message.ChatMessage.Message, 1, (_, count) => count + 1);

            // Update message times
            _messageTimes.Enqueue(message.Time);
            if (_messageTimes.Count > _maxStoredMessages)
            {
                _messageTimes.TryDequeue(out _);
            }

            // Recalculate bot-likeliness score
            CalculateBotScore();
            return this;
        }

        private void CalculateBotScore()
        {
            // Weights assigned to different metrics
            const double messageLengthWeight = 0.3;
            const double frequencyWeight = 0.3;
            const double repetitionWeight = 0.2;
            const double groupBehaviorWeight = 0.2;

            // Calculate individual scores
            var messageLengthConsistency = CalculateMessageLengthConsistency() * messageLengthWeight;
            var frequencyScore = CalculateMessageFrequencyScore() * frequencyWeight;
            var repetitionScore = CalculateRepetitionScore() * repetitionWeight;
            var groupBehaviorScore = _groupBehaviorScore * groupBehaviorWeight;

            // Total bot-likeliness score
            BotScore = messageLengthConsistency + frequencyScore + repetitionScore + groupBehaviorScore;
        }

        private double CalculateMessageLengthConsistency()
        {
            // Calculate standard deviation of message lengths
            var averageLength = (double)_totalLength / TotalMessages;
            var variance = _messageCounts
                .Select(kvp => Math.Pow(kvp.Key.Length - averageLength, 2) * kvp.Value)
                .Sum() / TotalMessages;

            var stdDev = Math.Sqrt(variance);

            // Invert standard deviation for consistency score (higher consistency = higher bot-likeliness)
            var consistencyScore = 100.0 - Math.Min(stdDev * 2, 100.0); // Adjust scaling as needed

            return consistencyScore;
        }

        private double CalculateMessageFrequencyScore()
        {
            // Messages per minute calculation
            var now = DateTime.UtcNow;
            var windowStart = now - _frequencyTimeWindow;

            var messagesInWindow = _messageTimes.Count(t => t >= windowStart);

            var messagesPerMinute = messagesInWindow / _frequencyTimeWindow.TotalMinutes;

            // Normalize frequency score (e.g., up to 100 for 30 messages per minute)
            var frequencyScore = Math.Min((messagesPerMinute / 30.0) * 100.0, 100.0);

            return frequencyScore;
        }

        private double CalculateRepetitionScore()
        {
            // Identify the maximum repetition count
            var maxRepetition = _messageCounts.Values.Max();

            // Normalize repetition score (e.g., up to 100 for 5 repetitions)
            var repetitionScore = Math.Min((maxRepetition / 5.0) * 100.0, 100.0);

            return repetitionScore;
        }

        public void IncreaseGroupBehaviorScore()
        {
            // Increase group behavior score, capped at 100
            _groupBehaviorScore = Math.Min(_groupBehaviorScore + 10, 100.0);

            // Recalculate bot-likeliness score
            CalculateBotScore();
        }
    }
    
    // Represents an entry in the recent messages queue
    public class MessageEntry
    {
        public string Username { get; set; }
        public string MessageText { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // Represents the result for a user
    public class BotLikelinessResult
    {
        public string Username { get; set; }
        public double LikelinessPercentage { get; set; }
    }

    // Represents a snapshot of top suspicious users at a specific time
    public class Snapshot
    {
        public DateTime Timestamp { get; set; }
        public List<SnapshotUser> Users { get; set; } = new();
    }

    // Represents a user in a snapshot
    public class SnapshotUser
    {
        public string Username { get; set; }
        public double LikelinessPercentage { get; set; }
    }
}
