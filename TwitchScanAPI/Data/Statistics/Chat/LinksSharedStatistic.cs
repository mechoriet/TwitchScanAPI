using System.Text.RegularExpressions;
using System.Threading;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class LinksSharedStatistic : IStatistic
    {
        public string Name => "LinksShared";
        private int _linkCount;
        private static readonly Regex LinkRegex = new(@"(http|https)://[^\s]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public object GetResult()
        {
            return _linkCount;
        }

        public void Update(ChannelMessage message)
        {
            if (LinkRegex.IsMatch(message.ChatMessage.Message))
            {
                Interlocked.Increment(ref _linkCount);
            }
        }
    }
}