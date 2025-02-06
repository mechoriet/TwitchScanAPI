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
    public class ChatHistoryStatistic : IStatistic
    {
        private const int MaxMessagesPerUser = 2000; // Limit to avoid memory overload

        // Stores chat history per user, each user has a list of messages
        private readonly ConcurrentDictionary<string, List<ChannelMessage>> _chatHistory = new();
        public string Name => "ChatHistory";

        public object GetResult()
        {
            var chatHistory = new List<ChatHistory>();

            // Convert the dictionary to a list of ChatHistory objects
            foreach (var (username, messages) in _chatHistory) chatHistory.Add(new ChatHistory(username, messages));

            return chatHistory;
        }

        public Task Update(ChannelMessage message)
        {
            var username = message.ChatMessage.Username.ToLowerInvariant();

            // Add the message to the user's chat history
            _chatHistory.AddOrUpdate(username,
                new List<ChannelMessage> { message }, // If no history, start a new list
                (_, existingMessages) =>
                {
                    lock (existingMessages) // Ensure thread safety when modifying the list
                    {
                        // Add the new message
                        existingMessages.Add(message);

                        // limit the number of messages stored
                        if (existingMessages.Count > MaxMessagesPerUser)
                            existingMessages.RemoveAt(0); // Remove the oldest message if limit is exceeded

                        return existingMessages;
                    }
                });

            return Task.CompletedTask;
        }
    }
}