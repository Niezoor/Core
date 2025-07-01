using UnityEngine;
using UnityEngine.Events;

namespace Core.Utilities.Events
{
    [CreateAssetMenu(fileName = "Event", menuName = "Core/Event")]
    public class EventSo : ScriptableObject
    {
        public UnityEvent Listener;

        public void Invoke()
        {
            Listener.Invoke();
        }
    }
}