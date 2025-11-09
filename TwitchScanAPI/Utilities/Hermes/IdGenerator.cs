using System;
using System.Linq;

namespace TwitchScanAPI.Utilities.Hermes;

public static class IdGenerator
{
    private static readonly char[] _chars = 
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    private static readonly Random _random = new Random();

    public static string MakeId()
    {
        return new string(Enumerable.Range(0, 21)
            .Select(_ => _chars[_random.Next(_chars.Length)])
            .ToArray());
    }
}