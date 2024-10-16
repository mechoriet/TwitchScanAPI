using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Chat;
using TwitchScanAPI.Models.Twitch.Chat;
using TwitchScanAPI.Models.Twitch.Statistics;

namespace TwitchScanAPI.Data.Statistics.Base
{
    public class StatisticsManager
    {
        private readonly Statistics _statistics = new();
        public bool PropagateEvents { get; set; } = true;

        public void Reset()
        {
            _statistics.Reset();
        }

        public async Task Update<TEvent>(TEvent eventData)
        {
            if (!PropagateEvents) return;
            await _statistics.Update(eventData);
        }

        public IDictionary<string, object> GetAllStatistics()
        {
            return _statistics.GetAllStatistics();
        }

        public object? GetStatistic(string name)
        {
            return _statistics.GetStatistic(name);
        }

        public List<ChatHistory> GetChatHistory(string? username = null)
        {
            var chatHistory = _statistics.GetStatistic("ChatHistory") as List<ChatHistory> ?? new List<ChatHistory>();

            if (string.IsNullOrEmpty(username))
            {
                return chatHistory.OrderBy(x => x.Time).ToList();
            }

            return chatHistory
                .Where(x => string.Equals(x.Username, username, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(x => x.Time)
                .ToList();
        }
    }
}