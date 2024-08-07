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
            }

            if (item is ISaveId saveId)
            {
                instance.AssignSaveIdInternal(item, saveId);
            }
        }

        public static void UnregisterItem(T item)
        {
            Debug.Log($"Unregister item {item}", item);
            FindInstance();
            if (instance == null) return;
            instance.Items.Remove(item);
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

        private void AssignSaveIdInternal(T item, ISaveId saveId)
        {
            int highestId = 0;
            foreach (var otherItem in instance.Items)
            {
                if (otherItem == item || otherItem is not ISaveId otherSaveId) continue;
                if (otherSaveId.SaveId > highestId)
                {
                    highestId = otherSaveId.SaveId;
                }

                if (otherSaveId.SaveId == saveId.SaveId)
                {
                    saveId.SaveId = 0;
                }
            }

            if (saveId.SaveId == 0)
            {
                saveId.SaveId = highestId + 1;
                Debug.Log($"Assign item {saveId} save id:{saveId.SaveId}");
            }
        }

        [Button]
        private void AssignSaveId()
        {
            foreach (var item in Items)
            {
                if (item == null) continue;
                if (item is ISaveId saveId && saveId.SaveId == 0)
                {
                    AssignSaveIdInternal(item, saveId);
                    EditorUtility.SetDirty(item);
                }
            }

            AssetDatabase.SaveAssets();
        }
#endif
    }
}