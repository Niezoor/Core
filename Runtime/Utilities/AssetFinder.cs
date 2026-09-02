using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Core.Utilities
{
    public static class AssetFinder
    {
        public static T Find<T>(string name) where T : Object
        {
#if UNITY_EDITOR
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name} {name}");
            if (guids.Length <= 0) return null;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<T>(path);
#else
            return null;
#endif
        }

        public static T Find<T>() where T : Object
        {
            return FindAll<T>()?.FirstOrDefault();
        }

        public static List<T> FindAll<T>() where T : Object
        {
            return FindAll<T>("");
        }

        public static List<T> FindAll<T>(string pattern) where T : Object
        {
#if UNITY_EDITOR
            var filter = $"t:{typeof(T).Name} {pattern}";
            var guids = AssetDatabase.FindAssets(filter);
            Debug.Log($"Find({filter}): {guids.Length})");
            if (guids.Length <= 0) return null;
            var list = new List<T>();
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                list.Add(AssetDatabase.LoadAssetAtPath<T>(path));
            }

            return list;
#else
            return null;
#endif
        }
    }
}