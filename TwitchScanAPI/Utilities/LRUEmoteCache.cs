using System;
using System.Collections.Generic;
using System.Threading;

namespace TwitchScanAPI.Utilities;

public class LruEmoteCache(int maxSize, int expireAfterHours)
{
    private readonly TimeSpan _expireAfter = TimeSpan.FromHours(expireAfterHours);
    private readonly Dictionary<string, LinkedListNode<CacheItem>> _cache = new(maxSize);
    private readonly LinkedList<CacheItem> _lruList = new();
    private readonly ReaderWriterLockSlim _lock = new();

    public CompiledEmotePattern? Get(string key)
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (!_cache.TryGetValue(key, out var node))
                return null;

            var item = node.Value;

            // Check if expired
            if (DateTime.UtcNow - item.LastAccessed > _expireAfter)
            {
                _lock.EnterWriteLock();
                try
                {
                    _cache.Remove(key);
                    _lruList.Remove(node);
                    return null;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            // Move to front (most recently used)
            _lock.EnterWriteLock();
            try
            {
                item.LastAccessed = DateTime.UtcNow;
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                return item.Pattern;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    public void Put(string key, CompiledEmotePattern pattern)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_cache.TryGetValue(key, out var existingNode))
            {
                // Update existing
                existingNode.Value.Pattern = pattern;
                existingNode.Value.LastAccessed = DateTime.UtcNow;
                _lruList.Remove(existingNode);
                _lruList.AddFirst(existingNode);
                return;
            }

            // Clean expired entries before adding new ones
            CleanExpiredEntries();

            // Remove LRU items if at capacity
            while (_cache.Count >= maxSize)
            {
                var lru = _lruList.Last;
                if (lru != null)
                {
                    _cache.Remove(lru.Value.Key);
                    _lruList.RemoveLast();
                }
            }

            // Add new item
            var newItem = new CacheItem(key, pattern, DateTime.UtcNow);
            var newNode = _lruList.AddFirst(newItem);
            _cache[key] = newNode;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void CleanExpiredEntries()
    {
        var now = DateTime.UtcNow;
        var toRemove = new List<LinkedListNode<CacheItem>>();

        // Find expired entries
        var current = _lruList.Last;
        while (current != null)
        {
            if (now - current.Value.LastAccessed > _expireAfter)
            {
                toRemove.Add(current);
                current = current.Previous;
            }
            else
            {
                break; // Since list is ordered by access time, we can stop here
            }
        }

// Remove expired entries
        foreach (var node in toRemove)
        {
            _cache.Remove(node.Value.Key);
            _lruList.Remove(node);
        }
    }

    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _cache.Clear();
            _lruList.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}