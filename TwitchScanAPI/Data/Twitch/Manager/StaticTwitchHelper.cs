using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TwitchScanAPI.Models.Twitch.Chat;
using TwitchScanAPI.Models.Twitch.Emotes;

namespace TwitchScanAPI.Data.Twitch.Manager
{
    public static class StaticTwitchHelper
    {
        public static void AddEmotesToMessage(ChannelMessage channelMessage, IEnumerable<MergedEmote>? emotes)
        {
            if (emotes == null) return;
            foreach (var emote in emotes)
            {
                var emoteRegex = new Regex($@"\b{Regex.Escape(emote.Name)}\b");
                if (!emoteRegex.IsMatch(channelMessage.ChatMessage.Message)) continue;

                var matches = emoteRegex.Matches(channelMessage.ChatMessage.Message);
                foreach (Match match in matches)
                {
                    channelMessage.ChatMessage.Emotes.Add(new TwitchEmote(emote.Id, emote.Name, emote.Url,
                        match));
                }
            }
        }
    }
}