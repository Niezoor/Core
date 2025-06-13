using UnityEngine;
using UnityEngine.Events;

namespace Core.Utilities.Events
{
    public class EventReceiver : MonoBehaviour
    {
        [SerializeField] private SoEvent soEvent;

        public UnityEvent Event;

        private void OnEnable()
        {
            soEvent.Event.AddListener(soEvent.Invoke);
        }

        private void OnDisable()
        {
            soEvent.Event.RemoveListener(soEvent.Invoke);
        }
    }
}