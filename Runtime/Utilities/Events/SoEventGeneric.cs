using UnityEngine.Events;

namespace Core.Utilities.Events
{
    public abstract class SoEventGeneric<T> : SoEvent
    {
        public new UnityEvent<T> Event;

        public void Invoke(T value)
        {
            Event.Invoke(value);
        }
    }
}