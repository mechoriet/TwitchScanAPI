using System;
using System.Collections.Generic;

namespace TwitchScanAPI.Data.Statistics.Utilities
{
    public static class StatisticsUtils
    {
        public static HashSet<string> GetNGrams(string input, int n)
        {
            var ngrams = new HashSet<string>();
            var span = input.AsSpan();
            
            for (var i = 0; i < input.Length - n + 1; i++) ngrams.Add(new string(span.Slice(i, n)));

            return ngrams;
        }

        public static double CalculateJaccardSimilarity(HashSet<string> set1, HashSet<string> set2)
        {
            if (set1.Count == 0 && set2.Count == 0) return 1.0;
            if (set1.Count == 0 || set2.Count == 0) return 0.0;

            int intersectionCount = 0;

            // Always iterate over the smaller set for speed
            if (set1.Count > set2.Count)
            {
                (set1, set2) = (set2, set1);
            }

            foreach (var item in set1)
            {
                if (set2.Contains(item))
                {
                    intersectionCount++;
                }
            }

            int unionCount = set1.Count + set2.Count - intersectionCount;

            return (double)intersectionCount / unionCount;
        }
    }
}