using System;
using System.Collections.Generic;
using Core.Save.Storages;
using Core.Utilities;
using Core.Utilities.Optimization;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Core.Save
{
    //[ExecuteAlways]
    public class Save : Singleton<Save>
    {
        public static Action OnLoaded;
        public static Action OnSynced;
        public static Action OnCleared;
        public static Action<string> OnError;
        public static Action OnSuccess;
        public static Action OnUnsupportedSave;
        public static Action<CloudSave> OnConflict;
        public static string ErrorMessage => Instance.Storage.ErrorMessage;
        public static bool HasError => Instance.Storage.HasError;
        public static bool IsLoaded => Instance.Storage.IsLoaded;
        public static bool IsUnsupported { get; private set; } = false;
        public static string Description { get; private set; }

        public static double PlayTime
        {
            get => Instance.playTime;
            set => Instance.playTime = value;
        }

        public Storage Storage;
        [ShowInInspector] private List<CloudSave> cloudSaves = new();

        [ShowInInspector] private double playTime = 0;

        private void Awake()
        {
            switch (SaveSettings.Instance.LocalStorageType.Value)
            {
                case StorageType.FileStorage:
                    Storage = gameObject.AddComponent<FileStorage>();
                    break;
                case StorageType.PlayerPrefsStorage:
                    Storage = gameObject.AddComponent<PlayerPrefsStorage>();
                    break;
                default:
                    Debug.Log($"Storage not supported {SaveSettings.Instance.LocalStorageType.Value}");
                    break;
            }

            if (Storage == null) return;
            Storage.OnLoaded += OnLoaded;
            Storage.OnLoaded += LoadPlayTime;
            Storage.OnSynced += OnSynced;
            Storage.OnCleared += OnCleared;
            Storage.OnError += OnError;
            Storage.OnSuccess += OnSuccess;
            Storage.Load(null);
        }

        [Button]
        private void GetOrCreateDefaultSave()
        {
            var any = Resources.FindObjectsOfTypeAll<Save>();
            foreach (var save in any)
            {
                Debug.Log($"found save {save} ({save.hideFlags})", save);
            }
        }

        private void Update()
        {
            playTime += TimeCache.deltaTime;
        }

        public static void Set(string key, string value)
        {
            if (IsUnsupported) return;
            Instance.Storage.Set(key, value);
        }

        public static string Get(string key)
        {
            return Instance?.Storage?.Get(key);
        }

        public static bool Remove(string key)
        {
            return Instance != null && Instance.Storage != null && Instance.Storage.Remove(key);
        }

        [Button]
        public static void Clear(Action onClear = null)
        {
            Instance.Storage.Clear(onClear);
        }

        [Button]
        public static void Sync()
        {
            Sync(null);
        }

        public static void Sync(Action onSynced)
        {
            if (IsUnsupported) return;
            Instance.Storage.Sync(TimeSpan.FromSeconds(PlayTime).Ticks, Description, onSynced);
        }

        public static void TriggerUnsupportedSave()
        {
            IsUnsupported = true;
            OnUnsupportedSave?.Invoke();
        }

        public static void RegisterCloudSave(CloudSave cloudSave)
        {
            Instance.cloudSaves.Add(cloudSave);
            cloudSave.onConflict += Instance.ShowConflict;
            cloudSave.onLoaded += Instance.CloudSaveLoaded;
        }

        private void ShowConflict(CloudSave cloudSave)
        {
            OnConflict?.Invoke(cloudSave);
        }

        private void CloudSaveLoaded(CloudSave cloudSave)
        { }

        private void LoadPlayTime()
        {
            playTime = TimeSpan.FromTicks(Storage.Data.PlayTimeTicks).TotalSeconds;
        }

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        private void OnApplicationPause(bool pause)
        {
            if (pause && IsLoaded) Sync();
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus && IsLoaded) Sync();
        }
#else
        private void OnApplicationQuit()
        {
            if (IsLoaded) Sync();
        }
#endif
    }
}