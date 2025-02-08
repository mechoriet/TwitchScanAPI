using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class LinksSharedStatistic : IStatistic
    {
        private static readonly Regex LinkRegex = new(@"(http|https)://[^\s]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private int _linkCount;
        public string Name => "LinksShared";

        public object GetResult()
        {
            return _linkCount;
        }

        public Task Update(ChannelMessage message)
        {
            if (LinkRegex.IsMatch(message.ChatMessage.Message)) Interlocked.Increment(ref _linkCount);
            return Task.CompletedTask;
        }
        
        public void Dispose()
        {
            _linkCount = 0;
        }
    }
}