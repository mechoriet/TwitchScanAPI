using System;
using System.Threading;
using TwitchScanAPI.Data.Statistics.Chat.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class MessageIntervalStatistic : IStatistic
    {
        public string Name => "MessageIntervalMs";

        private object _lastMessageTime;
        private long _totalIntervalTicks;
        private long _intervalCount;

        public object GetResult()
        {
            return _intervalCount <= 0 ? 0 : (new TimeSpan(_totalIntervalTicks / _intervalCount)).TotalMilliseconds;
        }

        public void Update(ChannelMessage message)
        {
            if (message == null) return;

            var currentTime = message.Time;

            var lastTime = (DateTime?)Interlocked.Exchange(ref _lastMessageTime, currentTime);

            if (!lastTime.HasValue) return;
            var interval = currentTime - lastTime.Value;
            Interlocked.Add(ref _totalIntervalTicks, interval.Ticks);
            Interlocked.Increment(ref _intervalCount);
        }
    }
}