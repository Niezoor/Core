using System;
using System.IO;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Core.Save.Storages
{
    public class FileStorage : Storage
    {
        [ShowInInspector] private static string Path => Application.persistentDataPath + "/local.save";

        public override void Load(Action onLoad)
        {
            if (File.Exists(Path))
            {
                var data = File.ReadAllText(Path);
                JsonUtility.FromJsonOverwrite(data, Data);
            }

            TriggerOnLoaded();
            onLoad?.Invoke();
        }

        public override void Sync(Action onSync)
        {
            File.WriteAllText(Path, JsonUtility.ToJson(Data));
            TriggerOnSynced();
            onSync?.Invoke();
        }

        public override void Clear(Action onClear)
        {
            File.Delete(Path);
            Data.data = new();
            TriggerOnCleared();
            onClear?.Invoke();
        }
    }
}