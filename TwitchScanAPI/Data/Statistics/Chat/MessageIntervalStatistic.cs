using System;
using System.Threading;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class MessageIntervalStatistic : StatisticBase
    {
        private long _intervalCount;
        private object? _lastMessageTime;
        private long _totalIntervalTicks;
        public override string Name => "MessageIntervalMs";

        protected override object ComputeResult()
        {
            return _intervalCount <= 0 ? 0 : new TimeSpan(_totalIntervalTicks / _intervalCount).TotalMilliseconds;
        }

        public Task Update(ChannelMessage message)
        {
            var currentTime = message.Time;
            var lastTime = (DateTime?)Interlocked.Exchange(ref _lastMessageTime, currentTime);
            if (lastTime.HasValue)
            {
                var interval = currentTime - lastTime.Value;
                Interlocked.Add(ref _totalIntervalTicks, interval.Ticks);
                Interlocked.Increment(ref _intervalCount);
            }

            HasUpdated = true;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            _intervalCount = 0;
            _totalIntervalTicks = 0;
            _lastMessageTime = null;
        }
    }
}