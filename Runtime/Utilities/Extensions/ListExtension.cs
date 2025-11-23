using System.Collections.Generic;
using System.Linq;

namespace Core.Utilities.Extensions
{
    public static class ListExtension
    {
        public static T Random<T>(this List<T> list)
        {
            var count = list.Count;
            if (count == 0) return default;
            if (count == 1) return list[0];
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        public static T Random<T>(this IEnumerable<T> source)
        {
            return source.ToList().Random();
        }

        public static bool IsNullOrEmpty<T>(this IList<T> list) => list == null || list.Count == 0;
    }
}