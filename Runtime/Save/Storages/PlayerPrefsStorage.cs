using System;
using UnityEngine;

namespace Core.Save.Storages
{
    public class PlayerPrefsStorage : Storage
    {
        public override void Load(Action onLoad)
        {
            JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString("save"), Data);
            TriggerOnLoaded();
            onLoad?.Invoke();
        }

        public override void Sync(Action onSync)
        {
            PlayerPrefs.SetString("save", JsonUtility.ToJson(Data));
            TriggerOnSynced();
            onSync?.Invoke();
        }

        public override void Clear(Action onClear)
        {
            PlayerPrefs.DeleteKey("save");
            Data.Data = new();
            TriggerOnCleared();
            onClear?.Invoke();
        }
    }
}