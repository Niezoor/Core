using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using Vector2 = System.Numerics.Vector2;

namespace Core.Input.CustomDevice
{
    public struct TouchscreenGamepadState : IInputStateTypeInfo
    {
        public FourCC format => new FourCC('T', 'J', 'O', 'Y');

        [InputControl(displayName = "Stick 1", layout = "Stick", usage = "Primary2DMotion")]
        public Vector2 Stick1;

        [InputControl(displayName = "Stick 2", layout = "Stick", usage = "Secondary2DMotion")]
        public Vector2 Stick2;

        [InputControl(displayName = "Button 1", layout = "Button", usages = new[] { "PrimaryAction", "Submit" })]
        public bool Button1;
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    [InputControlLayout(stateType = typeof(TouchscreenGamepadState), displayName = "Touchscreen Gamepad")]
    public class TouchscreenGamepad : InputDevice, IInputUpdateCallbackReceiver
    {
        public static TouchscreenGamepad current;

        public StickControl Stick1 { get; private set; }
        public StickControl Stick2 { get; private set; }
        public ButtonControl Button1 { get; private set; }

        static TouchscreenGamepad()
        {
            InputSystem.RegisterLayout<TouchscreenGamepad>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeInPlayer()
        { }

        public void OnUpdate()
        { }

        public override void MakeCurrent()
        {
            base.MakeCurrent();
            current = this;
        }

        protected override void FinishSetup()
        {
            Stick1 = GetChildControl<StickControl>("Stick1");
            Stick2 = GetChildControl<StickControl>("Stick2");
            Button1 = GetChildControl<ButtonControl>("Button1");
            base.FinishSetup();
        }
    }
}