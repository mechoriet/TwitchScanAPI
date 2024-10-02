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
            return _users.TryAdd(username, username);
        }

        public bool RemoveUser(string username)
        {
            return _users.TryRemove(username, out _);
        }

        public IEnumerable<string> GetUsers()
        {
            return _users.Keys;
        }
    }
}