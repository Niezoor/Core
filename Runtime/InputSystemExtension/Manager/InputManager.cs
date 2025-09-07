using System;
using Core.Utilities;
using Core.Utilities.Extensions;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;

namespace Core.InputSystemExtension.Manager
{
    [Serializable]
    public enum InputType
    {
        Touch,
        Gamepad,
        KeyboardAndMouse,
        Unknown,
    }

    public class InputManager : Singleton<InputManager>
    {
        public event Action OnControlsChanged;

        [ShowInInspector, ReadOnly] public InputType InputType { get; private set; } = InputType.Touch;

        [NonSerialized] private PlayerInput player;
        [NonSerialized] private Action<InputControl, InputEventPtr> m_UnpairedDeviceUsedDelegate;
        [NonSerialized] private Func<InputDevice, InputEventPtr, bool> m_PreFilterUnpairedDeviceUsedDelegate;
        [NonSerialized] private bool m_OnUnpairedDeviceUsedHooked;

        /*[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Initialize()
        {
            TryCreateDefault();
        }*/

        private void Awake()
        {
            player ??= gameObject.GetOrAddComponent<PlayerInput>();
            if (!InputSystemSettings.Instance.InputActionAsset) return;
            player.actions = InputSystemSettings.Instance.InputActionAsset;
            player.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            SetDefaultDevice();
            TriggerCurrentControls(player);
            StartListeningForUnpairedDeviceActivity();
            player.onControlsChanged += TriggerCurrentControls;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            StopListeningForUnpairedDeviceActivity();
            player.onControlsChanged -= TriggerCurrentControls;
        }

        private void SetDefaultDevice()
        {
            if (Touchscreen.current != null)
            {
                player.SwitchCurrentControlScheme(Touchscreen.current);
            }
            else if (Gamepad.current != null)
            {
                player.SwitchCurrentControlScheme(Gamepad.current);
            }
            else if (Keyboard.current != null && Mouse.current != null)
            {
                player.SwitchCurrentControlScheme(Keyboard.current, Mouse.current);
            }
        }

        private void TriggerCurrentControls(PlayerInput _)
        {
            switch (player.currentControlScheme)
            {
                case "Touch":
                    InputType = InputType.Touch;
                    break;
                case "Gamepad":
                    InputType = InputType.Gamepad;
                    break;
                case "Keyboard&Mouse":
                    InputType = InputType.KeyboardAndMouse;
                    break;
                default:
                    InputType = InputType.Unknown;
                    break;
            }

            OnControlsChanged?.Invoke();
        }

        private void StartListeningForUnpairedDeviceActivity()
        {
            if (m_OnUnpairedDeviceUsedHooked)
                return;
            if (m_UnpairedDeviceUsedDelegate == null)
                m_UnpairedDeviceUsedDelegate = OnUnpairedDeviceUsed;
            if (m_PreFilterUnpairedDeviceUsedDelegate == null)
                m_PreFilterUnpairedDeviceUsedDelegate = OnPreFilterUnpairedDeviceUsed;
            InputUser.onUnpairedDeviceUsed += m_UnpairedDeviceUsedDelegate;
            InputUser.onPrefilterUnpairedDeviceActivity += m_PreFilterUnpairedDeviceUsedDelegate;
            ++InputUser.listenForUnpairedDeviceActivity;
            m_OnUnpairedDeviceUsedHooked = true;
        }

        private void StopListeningForUnpairedDeviceActivity()
        {
            if (!m_OnUnpairedDeviceUsedHooked)
                return;
            InputUser.onUnpairedDeviceUsed -= m_UnpairedDeviceUsedDelegate;
            InputUser.onPrefilterUnpairedDeviceActivity -= m_PreFilterUnpairedDeviceUsedDelegate;
            --InputUser.listenForUnpairedDeviceActivity;
            m_OnUnpairedDeviceUsedHooked = false;
        }

        private void OnUnpairedDeviceUsed(InputControl control, InputEventPtr eventPtr)
        {
            // We only support automatic control scheme switching in single player mode.
            if (PlayerInput.all.Count > 1)
            {
                Debug.LogWarning($"We only support automatic control scheme switching in single player mode.");
                return;
            }

            // Prevent switching by escape key (back key on Android)
            if (control.path.Equals("/Keyboard/escape"))
                return;

            var device = control.device;
            //using (InputActionRebindingExtensions.DeferBindingResolution())
            using (var availableDevices = InputUser.GetUnpairedInputDevices())
            {
                // Put our device first in the list to make sure it's the first one picked for a match.
                if (availableDevices.Count > 1)
                {
                    var indexOfDevice = availableDevices.IndexOf(device);
                    Debug.Assert(indexOfDevice != -1, "Did not find unpaired device in list of unpaired devices");
                    availableDevices.SwapElements(0, indexOfDevice);
                }

                // Add all devices currently already paired to us. This avoids us preventing
                // control schemes switches because of devices we're looking for already being
                // paired to us.
                var currentDevices = player.devices;
                for (var i = 0; i < currentDevices.Count; ++i)
                    availableDevices.Add(currentDevices[i]);

                // Find the best control scheme to use.
                if (InputControlScheme.FindControlSchemeForDevices(availableDevices, player.actions.controlSchemes,
                        out var controlScheme, out var matchResult, mustIncludeDevice: device))
                {
                    try
                    {
                        Debug.Log($"Set player control scheme: {controlScheme} (by:{control})");
                        var devices = new InputDevice[matchResult.devices.Count];
                        for (var i = 0; i < matchResult.devices.Count; i++)
                        {
                            devices[i] = matchResult.devices[i];
                        }

                        player.SwitchCurrentControlScheme(controlScheme.name, devices);
                    }
                    finally
                    {
                        matchResult.Dispose();
                    }
                }
            }
        }

        private bool OnPreFilterUnpairedDeviceUsed(InputDevice device, InputEventPtr eventPtr)
        {
            // Early out if the device isn't usable with any of our control schemes.
            var actions = player.actions;
            return actions != null && actions.IsUsableWithDevice(device);
        }
    }
}