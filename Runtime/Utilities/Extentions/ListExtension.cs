using System.Collections.Generic;
using System.Linq;

namespace Core.Utilities
{
    public static class ListExtension
    {
        public static T Random<T>(this List<T> list)
        {
            var count = list.Count;
            if (count == 0) return default;
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        public static T Random<T>(this IEnumerable<T> source)
        {
            return source.ToList().Random();
        }
    }
}