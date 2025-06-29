using System; 
using System.Collections.Generic; 
using System.Text.RegularExpressions;
using TwitchScanAPI.Models.Twitch.Chat;
using TwitchScanAPI.Models.Twitch.Emotes;
using TwitchScanAPI.Utilities;

namespace TwitchScanAPI.Data.Twitch.Manager;
public static class StaticTwitchHelper
{
    private static readonly LruEmoteCache EmotePatternCache = new(maxSize: 10000, expireAfterHours: 2);

    public static void AddEmotesToMessage(ChannelMessage? channelMessage, IEnumerable<MergedEmote>? emotes)
    {
        if (emotes == null || string.IsNullOrEmpty(channelMessage?.ChatMessage?.Message))
            return;

        var message = channelMessage.ChatMessage.Message;
        var foundEmotes = new List<TwitchEmote>();
        
        foreach (var emote in emotes)
        {
            if (string.IsNullOrEmpty(emote.Name))
                continue;

            // Quick string search before expensive regex
            if (!ContainsEmote(message, emote.Name))
                continue;

            var pattern = GetOrCreatePattern(emote.Name);
            if (!pattern.HasValue)
                continue;

            ProcessEmoteMatches(message, emote, pattern.Value.Regex, foundEmotes);
        }

        // Single bulk add
        if (foundEmotes.Count > 0)
        {
            channelMessage.ChatMessage.Emotes.AddRange(foundEmotes);
        }
    }

    private static bool ContainsEmote(string message, string emoteName)
    {
        var index = message.IndexOf(emoteName, StringComparison.Ordinal);
        if (index == -1) return false;

        var emoteLength = emoteName.Length;
        var messageLength = message.Length;
        
        do
        {
            // Check word boundaries manually for better performance
            var prevChar = index > 0 ? message[index - 1] : ' ';
            var nextChar = index + emoteLength < messageLength ? message[index + emoteLength] : ' ';
            
            if (!char.IsLetterOrDigit(prevChar) && !char.IsLetterOrDigit(nextChar))
                return true;
                
            index = message.IndexOf(emoteName, index + 1, StringComparison.Ordinal);
        } while (index != -1);
        
        return false;
    }

    private static void ProcessEmoteMatches(string message, MergedEmote emote, Regex regex, List<TwitchEmote> results)
    {
        try
        {
            var match = regex.Match(message);
            while (match.Success)
            {
                results.Add(new TwitchEmote(emote.Id, emote.Name, emote.Url, match));
                match = match.NextMatch();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing emote '{emote.Name}': {ex.Message}");
        }
    }

    private static CompiledEmotePattern? GetOrCreatePattern(string emoteName)
    {
        // Try to get from LRU cache first
        var cached = EmotePatternCache.Get(emoteName);
        if (cached.HasValue)
            return cached;

        try
        {
            var pattern = $@"(?<!\S){Regex.Escape(emoteName)}(?!\S)";
            var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
            var compiledPattern = new CompiledEmotePattern(regex, emoteName);
            
            // Add to LRU cache
            EmotePatternCache.Put(emoteName, compiledPattern);
            return compiledPattern;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating regex for emote '{emoteName}': {ex.Message}");
            return null;
        }
    }
}