using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Channel
{
    public class BitsCheeredStatistic : StatisticBase
    {
        public override string Name => "BitsCheeredStatistic";

        private ConcurrentDictionary<string, int> _topBitDonators = new();

        protected override object ComputeResult() => _topBitDonators;

        public Task Update(ChannelMessage message)
        {
            if (message.ChatMessage.Bits <= 0) return Task.CompletedTask;
            _topBitDonators.AddOrUpdate(
                message.ChatMessage.Username,
                message.ChatMessage.Bits,
                (key, oldValue) => oldValue + message.ChatMessage.Bits
            );
            HasUpdated = true;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _topBitDonators = new ConcurrentDictionary<string, int>();
        }
    }
}