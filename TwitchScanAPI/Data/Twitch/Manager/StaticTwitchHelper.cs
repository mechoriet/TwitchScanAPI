using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TwitchScanAPI.Models.Twitch.Chat;
using TwitchScanAPI.Models.Twitch.Emotes;

namespace TwitchScanAPI.Data.Twitch.Manager
{
    public static class StaticTwitchHelper
    {
        private static readonly ConcurrentDictionary<string, Regex> EmoteRegexCache = new();

        public static void AddEmotesToMessage(ChannelMessage? channelMessage, IEnumerable<MergedEmote>? emotes)
        {
            if (emotes == null || channelMessage?.ChatMessage.Message == null)
                return;

            var message = channelMessage.ChatMessage.Message;

            foreach (var emote in emotes)
            {
                if (string.IsNullOrEmpty(emote.Name) || message.IndexOf(emote.Name, StringComparison.Ordinal) == -1)
                    continue;

                var emoteRegex = EmoteRegexCache.GetOrAdd(emote.Name, name =>
                {
                    try
                    {
                        var pattern = $@"(?<!\S){Regex.Escape(name)}(?!\S)";
                        return new Regex(pattern, RegexOptions.Compiled);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating regex for emote '{name}': {ex.Message}");
                        return new Regex(string.Empty); // Fallback to an empty regex
                    }
                });

                try
                {
                    var matches = emoteRegex.Matches(message);
                    if (matches.Count == 0)
                        continue;

                    // Bulk add emotes for this match
                    var emotesToAdd = matches.Select(match => 
                        new TwitchEmote(emote.Id, emote.Name, emote.Url, match)).ToList();
                    channelMessage.ChatMessage.Emotes.AddRange(emotesToAdd);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing emote '{emote.Name}': {ex.Message}");
                }
            }
        }
    }
}
