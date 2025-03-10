using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Annotations;
using TwitchScanAPI.Data.Statistics.Base;
using TwitchScanAPI.Models.Twitch.Chat;
using TwitchScanAPI.Models.Twitch.Statistics;

namespace TwitchScanAPI.Data.Statistics.Chat
{
    [IgnoreStatistic]
    public class ChatHistoryStatistic : StatisticBase
    {
        private const int MaxMessagesPerUser = 2000; // Limit to avoid memory overload

        // Stores chat history per user
        private ConcurrentDictionary<string, List<ChannelMessage>> _chatHistory = new();

        public override string Name => "ChatHistory";

        protected override object ComputeResult()
        {
            var chatHistory = new List<ChatHistory>();

            // Convert the dictionary to a list of ChatHistory objects
            foreach (var (username, messages) in _chatHistory)
                chatHistory.Add(new ChatHistory(username, messages));

            return chatHistory;
        }

        public Task Update(ChannelMessage message)
        {
            var username = message.ChatMessage.Username.ToLowerInvariant();

            // Add the message to the user's chat history
            _chatHistory.AddOrUpdate(username,
                key => [message],
                (key, existingMessages) =>
                {
                    lock (existingMessages) // Ensure thread safety when modifying the list
                    {
                        existingMessages.Add(message);
                        if (existingMessages.Count > MaxMessagesPerUser)
                            existingMessages.RemoveAt(0); // Remove the oldest message if limit is exceeded
                        return existingMessages;
                    }
                });

            HasUpdated = true;
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            _chatHistory = new ConcurrentDictionary<string, List<ChannelMessage>>();
        }
    }
}