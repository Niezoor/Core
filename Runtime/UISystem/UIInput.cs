using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && CORE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Core.UISystem
{
    public enum InputMode
    {
        Touch,
        Pointer,
        Keyboard,
        Gamepad,
    }

    [Flags]
    public enum InputModeMask
    {
        Touch = 1 << (int)InputMode.Touch,
        Pointer = 1 << (int)InputMode.Pointer,
        Keyboard = 1 << (int)InputMode.Keyboard,
        Gamepad = 1 << (int)InputMode.Gamepad,
        Navigation = Keyboard | Gamepad,
        All = Touch | Pointer | Keyboard | Gamepad,
    }

    /// <summary>
    /// How the player is using the UI right now - the last device used - and the back button. Keyboard and gamepad
    /// are navigation modes: the UI shows focus there; touch gets bigger targets; pointer gets hover.
    /// The panel root carries <c>ui-input-touch</c> / <c>-pointer</c> / <c>-keyboard</c> / <c>-gamepad</c> and
    /// <c>ui-input-navigation</c>.
    /// </summary>
    public static class UIInput
    {
        // Browsers and mobile platforms also send mouse events for a touch; they are not a switch to the mouse.
        private const float TouchMouseGrace = 0.5f;

        private static InputMode mode;
        private static float lastTouchTime = float.NegativeInfinity;

        public static event Action<InputMode> ModeChanged;

        public static InputMode Mode => mode;

        public static bool IsNavigation => mode is InputMode.Keyboard or InputMode.Gamepad;

        /// <summary>Esc, Android back or gamepad B was pressed this frame.</summary>
        public static bool BackPressed { get; private set; }

        /// <summary>A text field has focus, so key presses are typing, not navigation.</summary>
        internal static Func<bool> IsTyping;

        public static bool Matches(InputModeMask mask) => (mask & (InputModeMask)(1 << (int)mode)) != 0;

        public static void SetMode(InputMode value)
        {
            if (value == mode) return;
            mode = value;
            ModeChanged?.Invoke(value);
        }

        internal static void Update()
        {
            BackPressed = false;
#if ENABLE_INPUT_SYSTEM && CORE_INPUT_SYSTEM
            PollInputSystem();
#elif ENABLE_LEGACY_INPUT_MANAGER
            PollLegacyInput();
#endif
        }

        internal static void PressBackForTests() => BackPressed = true;

#if ENABLE_INPUT_SYSTEM && CORE_INPUT_SYSTEM
        private static void PollInputSystem()
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.press.wasPressedThisFrame)
            {
                lastTouchTime = Time.unscaledTime;
                SetMode(InputMode.Touch);
            }

            var mouse = Mouse.current;
            if (mouse != null && Time.unscaledTime - lastTouchTime > TouchMouseGrace &&
                (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
                 mouse.delta.ReadValue().sqrMagnitude > 4f || mouse.scroll.ReadValue().sqrMagnitude > 0.01f))
            {
                SetMode(InputMode.Pointer);
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                // Android's back button arrives as Escape.
                if (keyboard.escapeKey.wasPressedThisFrame) BackPressed = true;
                // Only navigation keys switch: a mouse player pressing a hotkey should not get focus rings.
                if ((keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame ||
                     keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame ||
                     keyboard.tabKey.wasPressedThisFrame) && IsTyping?.Invoke() != true)
                {
                    SetMode(InputMode.Keyboard);
                }
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.buttonEast.wasPressedThisFrame) BackPressed = true;
                if (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame ||
                    pad.buttonNorth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame ||
                    pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame ||
                    pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame ||
                    pad.dpad.ReadValue().sqrMagnitude > 0.25f || pad.leftStick.ReadValue().sqrMagnitude > 0.25f)
                {
                    SetMode(InputMode.Gamepad);
                }
            }
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        private static Vector3 lastMousePosition;

        private static void PollLegacyInput()
        {
            for (var i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).phase != TouchPhase.Began) continue;
                lastTouchTime = Time.unscaledTime;
                SetMode(InputMode.Touch);
            }

            var mousePosition = Input.mousePosition;
            var mouseMoved = (mousePosition - lastMousePosition).sqrMagnitude > 4f;
            lastMousePosition = mousePosition;
            if (Time.unscaledTime - lastTouchTime > TouchMouseGrace &&
                (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || mouseMoved))
            {
                SetMode(InputMode.Pointer);
            }

            if (Input.GetKeyDown(KeyCode.Escape)) BackPressed = true;
            if ((Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                 Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
                 Input.GetKeyDown(KeyCode.Tab)) && IsTyping?.Invoke() != true)
            {
                SetMode(InputMode.Keyboard);
            }

            for (var key = KeyCode.JoystickButton0; key <= KeyCode.JoystickButton19; key++)
            {
                if (!Input.GetKeyDown(key)) continue;
                SetMode(InputMode.Gamepad);
                break;
            }
        }
#endif

        internal static void ResetForTests() => ResetStatics();

        // Domain reload is off in Enter Play Mode settings, so nothing static may survive into the next session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            mode = Application.isMobilePlatform ? InputMode.Touch : InputMode.Pointer;
            lastTouchTime = float.NegativeInfinity;
            BackPressed = false;
            ModeChanged = null;
            IsTyping = null;
        }
    }
}
