using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.Utilities.Data
{
    public class ScriptableDataBase<T> : ScriptableObject where T : ScriptableObject
    {
        [InlineEditor()]
        public List<T> Items;

#if UNITY_EDITOR
        private static ScriptableDataBase<T> instance;

        public static void RegisterItem(T item)
        {
            FindInstance();
            if (instance == null) return;
            if (!instance.Items.Contains(item))
            {
                instance.Items.Add(item);
                if (item is ISaveId saveId)
                {
                    AssignSaveId(saveId);
                    EditorUtility.SetDirty(item);
                }
            }
        }

        private static void AssignSaveId(ISaveId saveId)
        {
            int highestId = 0;
            foreach (var item in instance.Items)
            {
                if (item is ISaveId otherSaveId)
                {
                    if (otherSaveId.SaveId > highestId)
                    {
                        highestId = otherSaveId.SaveId;
                    }
                }
            }

            saveId.SaveId = highestId + 1;
        }

        private static void FindInstance()
        {
            if (instance != null) return;
            var guids = AssetDatabase.FindAssets($"t:{typeof(ScriptableDataBase<T>).Name}");
            if (guids.Length > 1)
            {
                Debug.LogWarning(
                    $"Multiple instances of {typeof(ScriptableDataBase<T>).Name} found in project. It may cause unexpected results");
            }
            else if (guids.Length == 0)
            {
                Debug.LogWarning(
                    $"{typeof(ScriptableDataBase<T>).Name} not found. Please create it to register items.");
                return;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            instance = AssetDatabase.LoadAssetAtPath<ScriptableDataBase<T>>(path);
        }
#endif
    }
}