using System;
using System.Threading;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class MessageIntervalStatistic : IStatistic
    {
        private long _intervalCount;

        private object? _lastMessageTime;
        private long _totalIntervalTicks;
        public string Name => "MessageIntervalMs";

        public object GetResult()
        {
            return _intervalCount <= 0 ? 0 : new TimeSpan(_totalIntervalTicks / _intervalCount).TotalMilliseconds;
        }

        public Task Update(ChannelMessage message)
        {
            var currentTime = message.Time;

            var lastTime = (DateTime?)Interlocked.Exchange(ref _lastMessageTime, currentTime);
            if (!lastTime.HasValue) return Task.CompletedTask;

            var interval = currentTime - lastTime.Value;
            Interlocked.Add(ref _totalIntervalTicks, interval.Ticks);
            Interlocked.Increment(ref _intervalCount);
            return Task.CompletedTask;
        }
    }
}