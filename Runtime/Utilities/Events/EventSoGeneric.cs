using UnityEngine;
using UnityEngine.Events;

namespace Core.Utilities.Events
{
    public abstract class EventSoGeneric<T> : ScriptableObject
    {
        public UnityEvent<T> Listener;

        public void Invoke(T value)
        {
            Listener.Invoke(value);
        }
    }
}