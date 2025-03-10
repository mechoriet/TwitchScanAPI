using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Client.Models;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;
using TwitchScanAPI.Models.Twitch.Statistics;
using TwitchScanAPI.Services;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class PeakActivityPeriodStatistic : StatisticBase
    {
        public override string Name => "PeakActivityPeriods";
        private const int BucketSize = 1; // Grouping messages into 1-minute periods

        // Dictionaries for tracking message counts in different channel states
        private ConcurrentDictionary<DateTime, long> _messagesOverTime = new();
        private ConcurrentDictionary<DateTime, long> _bitsOverTime = new();
        private ConcurrentDictionary<DateTime, long> _slowOnlyMessagesOverTime = new();
        private ConcurrentDictionary<DateTime, long> _subOnlyMessagesOverTime = new();
        private ConcurrentDictionary<DateTime, long> _emoteOnlyMessagesOverTime = new();

        // Stores the current channel state
        private ChannelState? _channelState;

        /// <summary>
        /// Computes the result by merging the data and calculating the trend.
        /// </summary>
        protected override object ComputeResult()
        {
            // Merge dictionaries into a complete data set
            var completeData = _messagesOverTime
                .Concat(_subOnlyMessagesOverTime)
                .Concat(_emoteOnlyMessagesOverTime)
                .Concat(_slowOnlyMessagesOverTime)
                .GroupBy(kv => kv.Key)
                .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

            var trend = TrendService.CalculateTrend(
                completeData,
                d => d.Value,
                d => d.Key
            );

            return PeakActivityPeriods.Create(
                trend,
                _messagesOverTime,
                _bitsOverTime,
                _subOnlyMessagesOverTime,
                _emoteOnlyMessagesOverTime,
                _slowOnlyMessagesOverTime
            );
        }

        /// <summary>
        /// Updates the statistic using a new channel message.
        /// </summary>
        public Task Update(ChannelMessage message)
        {
            var dateTime = message.Time;

            // Round the time to the nearest minute based on BucketSize.
            var roundedMinutes = Math.Floor((double)dateTime.Minute / BucketSize) * BucketSize;
            var roundedTime = new DateTime(
                dateTime.Year,
                dateTime.Month,
                dateTime.Day,
                dateTime.Hour,
                (int)roundedMinutes,
                0
            );

            // Increment message count based on the current channel state.
            if (_channelState?.SubOnly == true)
                _subOnlyMessagesOverTime.AddOrUpdate(roundedTime, 1, (_, oldValue) => oldValue + 1);
            else if (_channelState?.EmoteOnly == true)
                _emoteOnlyMessagesOverTime.AddOrUpdate(roundedTime, 1, (_, oldValue) => oldValue + 1);
            else if (_channelState?.SlowMode is > 0)
                _slowOnlyMessagesOverTime.AddOrUpdate(roundedTime, 1, (_, oldValue) => oldValue + 1);
            else
                _messagesOverTime.AddOrUpdate(roundedTime, 1, (_, oldValue) => oldValue + 1);

            // Increment bits count if bits were used.
            if (message.ChatMessage.Bits > 0)
                _bitsOverTime.AddOrUpdate(roundedTime, message.ChatMessage.Bits,
                    (_, oldValue) => oldValue + message.ChatMessage.Bits);

            HasUpdated = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Updates the channel state, which affects how messages are tracked.
        /// </summary>
        public Task Update(ChannelState channelState)
        {
            _channelState = channelState;
            HasUpdated = true;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            _messagesOverTime = new ConcurrentDictionary<DateTime, long>();
            _bitsOverTime = new ConcurrentDictionary<DateTime, long>();
            _subOnlyMessagesOverTime = new ConcurrentDictionary<DateTime, long>();
            _emoteOnlyMessagesOverTime = new ConcurrentDictionary<DateTime, long>();
            _slowOnlyMessagesOverTime = new ConcurrentDictionary<DateTime, long>();
            _channelState = null;
        }
    }
}