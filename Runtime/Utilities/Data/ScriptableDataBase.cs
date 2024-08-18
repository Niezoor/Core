using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.Utilities.Data
{
    public abstract class ScriptableDataBase : ScriptableObject
    {
        public abstract void RegisterItem(ScriptableObject item);
        public abstract void UnregisterItem(ScriptableObject item);
    }

    public abstract class ScriptableDataBase<T> : ScriptableDataBase where T : ScriptableObject
    {
        [InlineEditor()]
        public List<T> Items;

#if UNITY_EDITOR
        public override void RegisterItem(ScriptableObject item)
        {
            var target = item as T;
            if (Items.Contains(target)) return;
            Items.Add((T)item);
            EditorUtility.SetDirty(this);
            if (item is ISaveId saveId)
            {
                AssignSaveId(target, saveId);
            }

            AssetDatabase.SaveAssets();
        }

        public override void UnregisterItem(ScriptableObject item)
        {
            Items.Remove((T)item);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        private void AssignSaveId(T item, ISaveId saveId)
        {
            int highestId = 0;
            foreach (var otherItem in Items)
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
                EditorUtility.SetDirty(item);
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
                    AssignSaveId(item, saveId);
                }
            }

            AssetDatabase.SaveAssets();
        }
#endif
    }
}