using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Core.Input.OnScreen
{
    public enum StickMode
    {
        Locked, // Locked stick position
        Fixed, // Initial touch position become stick position
        Free // Initial touch position become stick position and follow around without going beyond the set area of control
    }

    public class TouchscreenSticksController : OnScreenControl
    {
        public StickMode Mode = StickMode.Free;

        [InputControl(layout = "Vector2")] [SerializeField] private string inputControl;
        [SerializeField] private RectTransform area;
        [SerializeField] private RectTransform stick;
        [SerializeField] private RectTransform knob;

        [ShowInInspector, ReadOnly] private Vector2 startPosition;
        [ShowInInspector, ReadOnly] private Vector2 inputPosition;
        [ShowInInspector, ReadOnly] private Vector2 deltaPosition;

        private Vector3 originalStickPosition;
        private List<RaycastResult> raycastResults;
        private PointerEventData pointerEventData;
        private TouchControl currentTouch;
        private Camera uiCamera;

        protected override string controlPathInternal
        {
            get => inputControl;
            set => inputControl = value;
        }

        private void Start()
        {
            raycastResults = new List<RaycastResult>();
            originalStickPosition = stick.anchoredPosition;
            pointerEventData = new PointerEventData(EventSystem.current);
            var canvas = GetComponentInParent<Canvas>();
            var renderMode = canvas?.renderMode;
            if (renderMode != RenderMode.ScreenSpaceOverlay &&
                (renderMode != RenderMode.ScreenSpaceCamera || canvas?.worldCamera != null))
            {
                uiCamera = canvas?.worldCamera ?? Camera.main;
            }
        }

        private void Update()
        {
            UpdateInput();
        }

        private void UpdateInput()
        {
            if (Touchscreen.current == null) return;
            if (currentTouch == null)
            {
                FindTouch();
            }
            else
            {
                UpdateTouch();
            }
        }

        private void FindTouch()
        {
            for (var i = 0; i < Touchscreen.current.touches.Count; i++)
            {
                currentTouch = Touchscreen.current.touches[i];
                switch (currentTouch.phase.value)
                {
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                    case TouchPhase.Began:
                    {
                        if (IsStartPositionValid())
                        {
                            UpdateStick();
                            return;
                        }

                        break;
                    }
                    case TouchPhase.None:
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                    default:
                        break;
                }
            }

            currentTouch = null;
        }

        private void UpdateTouch()
        {
            switch (currentTouch.phase.value)
            {
                case TouchPhase.None:
                    break;
                case TouchPhase.Began:
                    if (Mode != StickMode.Locked)
                    {
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, currentTouch.startPosition.value,
                            uiCamera, out startPosition);
                    }

                    UpdateStick();
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    UpdateStick();
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                default:
                    ResetStick();
                    break;
            }
        }

        private bool IsStartPositionValid()
        {
            pointerEventData.position = currentTouch.startPosition.value;
            EventSystem.current.RaycastAll(pointerEventData, raycastResults);
            return raycastResults.Count > 0 && raycastResults[0].gameObject == area.gameObject;
        }

        private void UpdateStick()
        {
            if (!IsStartPositionValid())
            {
                ResetStick();
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, currentTouch.position.value,
                uiCamera, out inputPosition);
            deltaPosition = inputPosition - startPosition;
            SendValueToControl(deltaPosition);
            var magnitude = deltaPosition.magnitude;
            var maxMagnitude = stick.sizeDelta.x * 0.5f;
            if (Mode == StickMode.Free)
            {
                if (magnitude > maxMagnitude)
                {
                    startPosition += deltaPosition.normalized * (magnitude - maxMagnitude);
                    deltaPosition = Vector2.ClampMagnitude(deltaPosition, maxMagnitude);
                }
            }
            else
            {
                deltaPosition = Vector2.ClampMagnitude(deltaPosition, maxMagnitude);
            }

            stick.localPosition = startPosition;
            knob.localPosition = deltaPosition;
        }

        private void ResetStick()
        {
            currentTouch = null;
            SendValueToControl(Vector2.zero);
            stick.anchoredPosition = originalStickPosition;
            knob.localPosition = Vector3.zero;
        }
    }
}