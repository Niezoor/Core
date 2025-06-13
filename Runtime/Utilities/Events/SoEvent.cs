using UnityEngine;
using UnityEngine.Events;

namespace Core.Utilities.Events
{
    [CreateAssetMenu(fileName = "SoEvent", menuName = "Core/SoEvent")]
    public class SoEvent : ScriptableObject
    {
        public UnityEvent Event;

        public void Invoke()
        {
            Event.Invoke();
        }
    }
}