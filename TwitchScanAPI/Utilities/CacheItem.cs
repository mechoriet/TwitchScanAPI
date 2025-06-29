using System;

namespace TwitchScanAPI.Utilities;

public class CacheItem
{
    public string Key { get; }
    public CompiledEmotePattern Pattern { get; set; }
    public DateTime LastAccessed { get; set; }

    public CacheItem(string key, CompiledEmotePattern pattern, DateTime lastAccessed)
    {
        Key = key;
        Pattern = pattern;
        LastAccessed = lastAccessed;
    }
}