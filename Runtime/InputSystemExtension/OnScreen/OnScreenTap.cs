using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Core.InputSystemExtension.OnScreen
{
    [DefaultExecutionOrder(-1000)]
    public class OnScreenTap : OnScreenControl
    {
        [InputControl(layout = "Button")] [SerializeField] private string inputControl;
        [SerializeField] private int touchIndex;

        private PointerEventData pointerEventData;
        private List<RaycastResult> raycastResults;
        private bool isDown = false;

        protected override string controlPathInternal
        {
            get => inputControl;
            set => inputControl = value;
        }

        private void Awake()
        {
            raycastResults = new List<RaycastResult>();
            pointerEventData = new PointerEventData(EventSystem.current);
        }

        private void Update()
        {
            UpdateInput();
        }

        private void UpdateInput()
        {
            if (Touchscreen.current == null) return;
            switch (Touchscreen.current.touches[touchIndex].phase.value)
            {
                case TouchPhase.None:
                    break;
                case TouchPhase.Began:
                    Press();
                    break;
                case TouchPhase.Moved:
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    Release();
                    break;
                case TouchPhase.Stationary:
                    break;
                default:
                    Debug.LogError($"Touch phase not handled: {Touchscreen.current.touches[touchIndex].phase.value}");
                    break;
            }
        }

        private void Press()
        {
            if (!isDown && IsPositionValid(Touchscreen.current.touches[touchIndex].position.value))
            {
                Debug.LogWarning($"PRESS");
                isDown = true;
                SendValueToControl(1f);
            }
        }

        private void Release()
        {
            if (isDown)
            {
                isDown = false;
                SendValueToControl(0f);
            }
        }

        private bool IsPositionValid(Vector2 position)
        {
            pointerEventData.position = position;
            EventSystem.current.RaycastAll(pointerEventData, raycastResults);
            return raycastResults.Count > 0 && raycastResults[0].gameObject == gameObject;
        }
    }
}