using System.Collections.Generic;
using System.Linq;

namespace TwitchScanAPI.Data.Statistics.Utilities
{
    public class StatisticsUtils
    {
        public static HashSet<string> GetNGrams(string input, int n)
        {
            var ngrams = new HashSet<string>();
            var length = input.Length;

            for (var i = 0; i < length - n + 1; i++)
            {
                ngrams.Add(input.Substring(i, n));
            }

            return ngrams;
        }

        public static double CalculateJaccardSimilarity(HashSet<string> set1, HashSet<string> set2)
        {
            var intersectionCount = set1.Intersect(set2).Count();
            var unionCount = set1.Union(set2).Count();

            return unionCount == 0 ? 0 : (double)intersectionCount / unionCount;
        }
    }
}