using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Core.Save
{
    [Serializable]
    public abstract class SaveData
    {
        protected abstract string Key { get; }
        public abstract int ClassVersion { get; }

        public int version = 0;

        public static T Create<T>() where T : SaveData, new()
        {
            var save = new T();
            save.Load();
            return save;
        }

        protected abstract void FixVersion();

        private void CheckVersion()
        {
            if (version < ClassVersion)
            {
                FixVersion();
            }
            else if (version > ClassVersion)
            {
                MainStorage.TriggerUnsupportedSave();
            }
        }

        [Button]
        public void Save()
        {
            MainStorage.Set(Key, JsonUtility.ToJson(this));
        }

        [Button]
        public void Load()
        {
            JsonUtility.FromJsonOverwrite(MainStorage.Get(Key), this);
        }

        public void SaveAndSync()
        {
            Save();
            MainStorage.Sync();
        }
    }
}