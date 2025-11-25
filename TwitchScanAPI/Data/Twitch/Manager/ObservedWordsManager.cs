using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace TwitchScanAPI.Data.Twitch.Manager
{
    public class ObservedWordsManager
    {
        private readonly HashSet<string> _wordsToObserve = new(StringComparer.OrdinalIgnoreCase);
        private Regex? _observePatternRegex;
        private readonly Lock _lockObject = new();

        // Use StringBuilder pool to reduce allocations when building regex patterns
        private static readonly ObjectPool<StringBuilder> StringBuilderPool = new(() => new StringBuilder(256), 4);

        public void AddTextToObserve(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var trimmed = text.Trim();
            if (trimmed.Length == 0) return;

            lock (_lockObject)
            {
                if (_wordsToObserve.Add(trimmed))
                    UpdateRegex();
            }
        }

        private void UpdateRegex()
        {
            if (_wordsToObserve.Count == 0)
            {
                _observePatternRegex = null;
                return;
            }

            var stringBuilder = StringBuilderPool.Get();
            try
            {
                // Build pattern more efficiently
                var first = true;
                foreach (var word in _wordsToObserve)
                {
                    if (!first)
                        stringBuilder.Append('|');
                    else
                        first = false;
                    stringBuilder.Append(Regex.Escape(word));
                }

                var pattern = stringBuilder.ToString();
                _observePatternRegex = new Regex($@"\b({pattern})\b",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating regex pattern: {ex.Message}");
                _observePatternRegex = null;
            }
            finally
            {
                stringBuilder.Clear();
                StringBuilderPool.Return(stringBuilder);
            }
        }

        public bool IsMatch(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;

            lock (_lockObject)
            {
                return _observePatternRegex?.IsMatch(message) ?? false;
            }
        }
    }

    // Simple object pool implementation for better performance
    internal class ObjectPool<T> where T : class
    {
        private readonly Func<T> _factory;
        private readonly int _maxSize;
        private readonly T[] _pool;
        private int _count;
        private readonly Lock _lock = new();

        public ObjectPool(Func<T> factory, int maxSize = 10)
        {
            _factory = factory;
            _maxSize = maxSize;
            _pool = new T[maxSize];
            _count = 0;
        }

        public T Get()
        {
            lock (_lock)
            {
                if (_count > 0)
                {
                    return _pool[--_count]!;
                }
            }
            return _factory();
        }

        public void Return(T obj)
        {
            lock (_lock)
            {
                if (_count < _maxSize)
                {
                    _pool[_count++] = obj;
                }
            }
        }
    }
}
