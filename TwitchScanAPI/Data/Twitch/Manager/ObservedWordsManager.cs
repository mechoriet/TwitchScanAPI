using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace TwitchScanAPI.Data.Twitch.Manager
{
    public class ObservedWordsManager
    {
        private readonly HashSet<string> _wordsToObserve = new(StringComparer.OrdinalIgnoreCase);
        private Regex? _observePatternRegex;
        private readonly Lock _lockObject = new();

        public void AddTextToObserve(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            lock (_lockObject)
            {
                if (_wordsToObserve.Add(text.Trim())) 
                    UpdateRegex();
            }
        }

        private void UpdateRegex()
        {
            if (_wordsToObserve.Count != 0)
            {
                try
                {
                    var pattern = string.Join("|", _wordsToObserve.Select(Regex.Escape));
                    _observePatternRegex = new Regex($@"\b({pattern})\b", 
                        RegexOptions.IgnoreCase | RegexOptions.Compiled);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating regex pattern: {ex.Message}");
                    _observePatternRegex = null;
                }
            }
            else
            {
                _observePatternRegex = null;
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
}