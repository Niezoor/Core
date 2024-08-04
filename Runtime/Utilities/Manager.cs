using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Core.Utilities
{
    public class Manager<T> : SingletonPrefab<Manager<T>> where T : Component
    {
        public Action<T> OnRegistered;
        public Action<T> OnUnregistered;

        [ShowInInspector] public readonly HashSet<T> Registered = new();
        public int Count { get; private set; }

        public virtual void Register(T component)
        {
            Registered.Add(component);
            Count++;
            OnRegistered?.Invoke(component);
        }

        public virtual void Unregister(T component)
        {
            Registered.Remove(component);
            Count--;
            OnUnregistered?.Invoke(component);
        }
    }
}