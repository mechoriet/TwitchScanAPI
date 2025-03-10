using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class LinksSharedStatistic : StatisticBase
    {
        private static readonly Regex LinkRegex = new(@"(http|https)://[^\s]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private int _linkCount;
        public override string Name => "LinksShared";

        protected override object ComputeResult()
        {
            return _linkCount;
        }

        public Task Update(ChannelMessage message)
        {
            if (LinkRegex.IsMatch(message.ChatMessage.Message))
                Interlocked.Increment(ref _linkCount);

            HasUpdated = true;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            _linkCount = 0;
        }
    }
}