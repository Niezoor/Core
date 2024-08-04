using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Core.Save.Storages
{
    public abstract class Storage : MonoBehaviour
    {
        public event Action OnLoaded;
        public event Action OnSynced;
        public event Action OnCleared;
        public event Action<string> OnError;
        public event Action OnSuccess;

        public bool IsLoaded { get; private set; } = false;
        public bool HasError { get; private set; } = false;
        public string ErrorMessage { get; private set; }
        [ShowInInspector] public readonly StorageData Data = new();
        public abstract void Load(Action onLoad);

        public void Set(string key, string value)
        {
            Data.data[key] = value;
        }

        public string Get(string key)
        {
            return Data.data.TryGetValue(key, out var value) ? value : string.Empty;
        }

        public bool Remove(string key)
        {
            return Data.data.Remove(key);
        }

        public void Sync(long playTimeTicks, string desc, Action onSync)
        {
            Data.playTimeTicks = playTimeTicks;
            Data.description = desc;
            Sync(onSync);
        }

        public abstract void Sync(Action onSync);
        public abstract void Clear(Action onClear);

        protected void TriggerOnLoaded()
        {
            IsLoaded = true;
            OnLoaded?.Invoke();
        }

        protected void TriggerOnSynced()
        {
            OnSynced?.Invoke();
        }

        protected void TriggerOnCleared()
        {
            OnCleared?.Invoke();
        }

        protected void TriggerError(string message)
        {
            HasError = true;
            ErrorMessage = message;
            OnError?.Invoke(message);
        }

        protected void TriggerSuccess()
        {
            HasError = false;
            OnSuccess?.Invoke();
        }
    }
}