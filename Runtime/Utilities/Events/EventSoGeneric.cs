using UnityEngine.Events;

namespace Core.Utilities.Events
{
    public abstract class EventSoGeneric<T> : EventSo
    {
        public new UnityEvent<T> Listener;

        public void Invoke(T value)
        {
            Listener.Invoke(value);
        }
    }
}