using System.Collections.Generic;

namespace Core.Utilities
{
    public static class ListExtension
    {
        public static T Random<T>(this List<T> list)
        {
            return list[UnityEngine.Random.Range(0, list.Count)];
        }
    }
}