using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class UniqueWordsStatistic : StatisticBase
    {
        private static readonly Regex WordRegex = new(@"\b\w+\b", RegexOptions.Compiled);
        private ConcurrentDictionary<string, byte> _uniqueWords = new();

        public override string Name => "UniqueWords";

        protected override object ComputeResult()
        {
            return _uniqueWords.Count;
        }

        public Task Update(ChannelMessage message)
        {
            var words = WordRegex.Matches(message.ChatMessage.Message)
                .Select(m => m.Value.ToLowerInvariant().Trim())
                .Where(w => !string.IsNullOrWhiteSpace(w));

            foreach (var word in words)
                _uniqueWords.TryAdd(word, 0);

            HasUpdated = true;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            _uniqueWords = new ConcurrentDictionary<string, byte>();
        }
    }
}