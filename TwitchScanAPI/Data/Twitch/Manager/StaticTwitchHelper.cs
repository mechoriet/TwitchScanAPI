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

        public static void AddEmotesToMessage(ChannelMessage channelMessage, IEnumerable<MergedEmote>? emotes)
        {
            if (emotes == null)
                return;

            var message = channelMessage.ChatMessage.Message;

            foreach (var emote in emotes)
            {
                if (message.IndexOf(emote.Name, StringComparison.Ordinal) == -1)
                    continue;

                var emoteRegex = EmoteRegexCache.GetOrAdd(emote.Name, name =>
                {
                    var pattern = $@"(?<!\S){Regex.Escape(name)}(?!\S)";
                    return new Regex(pattern, RegexOptions.Compiled);
                });

                var matches = emoteRegex.Matches(message);
                if (matches.Count == 0)
                    continue;

                // Bulk add emotes for this match
                var emotesToAdd = matches.Select(match => 
                    new TwitchEmote(emote.Id, emote.Name, emote.Url, match)).ToList();
                channelMessage.ChatMessage.Emotes.AddRange(emotesToAdd);
            }
        }
    }
}