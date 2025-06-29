using System.Text.RegularExpressions;

namespace TwitchScanAPI.Utilities;

public struct CompiledEmotePattern(Regex regex, string emoteName)
{
    public readonly Regex Regex = regex;
    public readonly string EmoteName = emoteName;
    public readonly int EmoteNameLength = emoteName.Length;
}