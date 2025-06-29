using UnityEngine;
using UnityEngine.Events;

namespace Core.Utilities.Events
{
    public class EventReceiver : MonoBehaviour
    {
        [SerializeField] private EventSo eventSo;

        public UnityEvent Event;

        private void OnEnable()
        {
            eventSo.Event.AddListener(eventSo.Invoke);
        }

        private void OnDisable()
        {
            eventSo.Event.RemoveListener(eventSo.Invoke);
        }
    }
}