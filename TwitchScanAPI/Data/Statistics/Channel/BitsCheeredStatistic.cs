using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Channel
{
    public class BitsCheeredStatistic : IStatistic
    {
        public string Name => "BitsCheeredStatistic";
        
        private readonly ConcurrentDictionary<string, int> _topBitDonators = new();
        
        public object GetResult()
        {
            return _topBitDonators.ToList();
        }
        
        public Task Update(ChannelMessage message)
        {
            if (message.ChatMessage.Bits > 0)
                _topBitDonators.AddOrUpdate(message.ChatMessage.Username, message.ChatMessage.Bits, (key, oldValue) => oldValue + message.ChatMessage.Bits);

            return Task.CompletedTask;
        }
        
        public void Dispose()
        {
            _topBitDonators.Clear();
        }
    }
}