using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    public static class Finder
    {
        /// <summary>
        /// Find asset of type T
        /// </summary>
        /// <param name="filter">optional FindAssets filter</param>
        /// <typeparam name="T">Type</typeparam>
        /// <returns></returns>
        public static T FindAsset<T>(string filter = null) where T : Object
        {
            var searchString = $"t:{typeof(T).Name} {filter}";
            var guids = AssetDatabase.FindAssets(searchString);
            Debug.Log($"Searching:{searchString}");
            if (guids.Length == 0) return null;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Debug.Log($"AssetDatabase.FindAssets(\"t:{typeof(T)} {filter}\")={asset} (total:{guids.Length})", asset);
            return asset;
        }

        /// <summary>Finds prefabs with component T or its children.</summary>
        public static List<T> FindPrefabsWithComponent<T>(string filter = null) where T : Component
        {
            var prefabsFound = new List<T>();
            var allPrefabGUIDs = AssetDatabase.FindAssets($"t:Prefab {filter}");

            foreach (var guid in allPrefabGUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                var component = prefab.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    prefabsFound.Add(component);
                }
            }

            return prefabsFound;
        }
    }
}