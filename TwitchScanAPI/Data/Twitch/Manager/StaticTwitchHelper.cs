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
                // Custom regex to match emote names, allowing for emotes with non-alphanumeric characters
                var emoteRegex = new Regex($@"(?<!\S){Regex.Escape(emote.Name)}(?!\S)");
        
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