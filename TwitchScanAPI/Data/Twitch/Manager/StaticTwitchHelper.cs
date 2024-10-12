using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TwitchScanAPI.Models.Twitch.Chat;

namespace TwitchScanAPI.Data.Twitch.Manager
{
    public static class StaticTwitchHelper
    {
        public static void AddEmotesToMessage(ChannelMessage channelMessage, IEnumerable<dynamic>? emotes,
            Func<dynamic, string> getCode, Func<dynamic, string> getId, Func<dynamic, string> getUrl)
        {
            if (emotes == null) return;
            foreach (var emote in emotes)
            {
                var emoteRegex = new Regex($@"\b{Regex.Escape(getCode(emote))}\b");
                if (!emoteRegex.IsMatch(channelMessage.ChatMessage.Message)) continue;

                var matches = emoteRegex.Matches(channelMessage.ChatMessage.Message);
                foreach (Match match in matches)
                {
                    channelMessage.ChatMessage.Emotes.Add(new TwitchEmote(getId(emote), getCode(emote), getUrl(emote),
                        match));
                }
            }
        }
    }
}