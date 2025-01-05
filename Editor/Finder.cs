using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    public static class Finder
    {
        public static T FindAsset<T>(string name) where T : Object
        {
            var searchString = $"t:{typeof(T).Name} {name}";
            var guids = AssetDatabase.FindAssets(searchString);
            Debug.Log($"Searching:{searchString}");
            if (guids.Length == 0) return null;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Debug.Log($"AssetDatabase.FindAssets(\"t:{typeof(T)} {name}\")={asset} (total:{guids.Length})", asset);
            return asset;
        }
    }
}