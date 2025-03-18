using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace TwitchScanAPI.Data.Twitch.Manager
{
    public class UserManager
    {
        private readonly ConcurrentDictionary<string, string> _users = new(StringComparer.OrdinalIgnoreCase);

        public bool AddUser(string username)
        {
            return !string.IsNullOrWhiteSpace(username) && _users.TryAdd(username.Trim(), username);
        }

        public bool RemoveUser(string username)
        {
            return !string.IsNullOrWhiteSpace(username) && _users.TryRemove(username.Trim(), out _);
        }

        public IEnumerable<string> GetUsers()
        {
            return _users.Keys;
        }

        public int Count => _users.Count;
    }
}