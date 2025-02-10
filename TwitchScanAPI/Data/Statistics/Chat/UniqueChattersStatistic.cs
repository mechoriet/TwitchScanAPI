using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    public class UniqueChattersStatistic : IStatistic
    {
        private readonly ConcurrentDictionary<string, byte> _uniqueChatters = new();
        public string Name => "UniqueChatters";

        public object GetResult()
        {
            // Return the count of unique chatters
            return _uniqueChatters.Count;
        }

        public Task Update(ChannelMessage message)
        {
            // Extract the username from the message and add it to the dictionary if not already present
            var username =
                message.ChatMessage.Username.ToLowerInvariant().Trim(); // Normalize to avoid case sensitivity issues

            _uniqueChatters.TryAdd(username, 0); // Add the username if it's not already present

            return Task.CompletedTask;
        }
        
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _uniqueChatters.Clear();
        }
    }
}