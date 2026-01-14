using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Core.Utilities
{
    public class Manager<T1, T2> : PersistentSingleton<T1> where T1 : MonoBehaviour
    {
        public Action<T2> OnRegistered;
        public Action<T2> OnUnregistered;

        [ShowInInspector] public readonly HashSet<T2> Registered = new();
        public int Count { get; private set; }

        public virtual void Register(T2 component)
        {
            Registered.Add(component);
            Count++;
            OnRegistered?.Invoke(component);
        }

        public virtual void Unregister(T2 component)
        {
            Registered.Remove(component);
            Count--;
            OnUnregistered?.Invoke(component);
        }
    }
}