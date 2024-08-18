using UnityEditor;
using UnityEngine;

namespace Core.Utilities.Data
{
    public class ScriptableData<T> : ScriptableObject, ISaveId where T : ScriptableDataBase
    {
        [SerializeField] private int id;
        public int SaveId
        {
            get => id;
            set => id = value;
        }

#if UNITY_EDITOR
        private static T dataBase;

        protected virtual void Reset()
        {
            SaveId = 0;
            RegisterItem();
        }

        protected virtual void Awake()
        {
            RegisterItem();
        }

        private void RegisterItem()
        {
            FindInstance();
            if (dataBase != null)
            {
                dataBase.RegisterItem(this);
            }
        }

        private static void FindInstance()
        {
            if (dataBase != null) return;
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length > 1)
            {
                Debug.LogWarning(
                    $"Multiple instances of {typeof(T).Name} found in project. It may cause unexpected results");
            }
            else if (guids.Length == 0)
            {
                Debug.LogWarning(
                    $"{typeof(T).Name} not found. Please create it to register items.");
                return;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            dataBase = AssetDatabase.LoadAssetAtPath<T>(path);
        }
#endif
    }
}