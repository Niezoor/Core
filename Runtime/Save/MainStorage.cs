using System;
using Core.Save.Storages;
using Core.Utilities;
using Core.Utilities.Optimization;
using Sirenix.OdinInspector;

namespace Core.Save
{
    public class MainStorage : Singleton<MainStorage>
    {
        public static Action OnLoaded;
        public static Action OnSynced;
        public static Action OnCleared;
        public static Action<string> OnError;
        public static Action OnSuccess;
        public static Action OnUnsupportedSave;
        public static string ErrorMessage => Instance.storage.ErrorMessage;
        public static bool HasError => Instance.storage.HasError;
        public static bool IsLoaded => Instance.storage.IsLoaded;
        public static bool IsUnsupported { get; private set; } = false;
        public static string Description { get; private set; }
        public static double PlayTime
        {
            get => Instance.playTime;
            set => Instance.playTime = value;
        }

        public Storage storage;

        [ShowInInspector] private double playTime = 0;

        private void Awake()
        {
            storage = Instance.gameObject.AddComponent<PlayerPrefsStorage>();
            storage.OnLoaded += OnLoaded;
            storage.OnLoaded += LoadPlayTime;
            storage.OnSynced += OnSynced;
            storage.OnCleared += OnCleared;
            storage.OnError += OnError;
            storage.OnSuccess += OnSuccess;
            storage.Load(null);
        }

        private void Update()
        {
            playTime += TimeCache.DeltaTime;
        }

        public static void Set(string key, string value)
        {
            if (IsUnsupported) return;
            Instance.storage.Set(key, value);
        }

        public static string Get(string key)
        {
            return Instance.storage.Get(key);
        }

        public static bool Remove(string key)
        {
            return Instance.storage.Remove(key);
        }

        [Button]
        public static void Clear(Action onClear = null)
        {
            Instance.storage.Clear(onClear);
        }

        [Button]
        public static void Sync()
        {
            Sync(null);
        }

        public static void Sync(Action onSynced)
        {
            if (IsUnsupported) return;
            Instance.storage.Sync(TimeSpan.FromSeconds(PlayTime).Ticks, Description, onSynced);
        }

        public static void TriggerUnsupportedSave()
        {
            IsUnsupported = true;
            OnUnsupportedSave?.Invoke();
        }

        private void LoadPlayTime()
        {
            playTime = TimeSpan.FromTicks(storage.Data.playTimeTicks).TotalSeconds;
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