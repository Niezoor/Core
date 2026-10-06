using System;
using System.Collections.Generic;
using Core.Display;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM && CORE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Core.UISystem
{
    /// <summary>
    /// In-game debug menu: a runtime inspector for registered objects, settings, the scene hierarchy, the screen
    /// profile and time. Opened with the backquote key (`), both sticks pressed together, or a three-finger tap.
    /// Development builds and the editor only, unless <see cref="UISystemSettings"/> allows it in release.
    /// </summary>
    public static class DebugMenu
    {
        private static readonly List<(string path, Func<object> target)> entries = new();
        private static bool? enabledOverride;
        private static UIDocument document;
        private static PanelSettings panelSettings;
        private static DebugMenuView view;

        public static event Action<bool> OpenChanged;

        /// <summary>Whether the shortcuts open it. Default: development build, editor, or the release option.</summary>
        public static bool Enabled
        {
            get => enabledOverride ?? (Debug.isDebugBuild ||
                                       UISystemSettings.TryGet(out var settings) && settings.DebugMenuInRelease);
            set => enabledOverride = value;
        }

        public static bool IsOpen { get; private set; }

        /// <summary>Registered pages by path; "Cheats/Economy" shows Economy inside a Cheats folder.</summary>
        public static IReadOnlyList<(string path, Func<object> target)> Entries => entries;

        /// <summary>Adds a page inspecting the object; the same path replaces the previous page.</summary>
        public static void Register(string path, object target) => Register(path, () => target);

        /// <summary>Adds a page whose object is looked up each time the page opens.</summary>
        public static void Register(string path, Func<object> target)
        {
            Unregister(path);
            entries.Add((path, target));
            view?.RefreshRoot();
        }

        public static void Unregister(string path)
        {
            entries.RemoveAll(e => e.path == path);
            view?.RefreshRoot();
        }

        /// <summary>Opens the menu, at the root or straight at an object's page.</summary>
        public static void Open(object target = null)
        {
            EnsureView();
            if (target != null) view.OpenObject(target);
            if (IsOpen) return;
            IsOpen = true;
            document.rootVisualElement.style.display = DisplayStyle.Flex;
            view.OnOpened();
            OpenChanged?.Invoke(true);
        }

        public static void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            document.rootVisualElement.style.display = DisplayStyle.None;
            OpenChanged?.Invoke(false);
        }

        public static void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        /// <summary>Back inside the menu: the previous page, or closing it at the root.</summary>
        public static void Back()
        {
            if (!IsOpen) return;
            if (!view.Back()) Close();
        }

        /// <summary>The DontDestroyOnLoad scene, which has no other public handle.</summary>
        internal static UnityEngine.SceneManagement.Scene PersistentScene =>
            document ? document.gameObject.scene : default;

        internal static bool IsTyping =>
            IsOpen && Panels.IsTextInput(document.rootVisualElement.panel?.focusController.focusedElement as VisualElement);

        internal static bool IsPointerOver(Vector2 screenPosition)
        {
            if (!IsOpen) return false;
            var panel = document.rootVisualElement.panel;
            if (panel == null) return false;
            var point = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            var picked = panel.Pick(point);
            return picked != null && view != null && view.Contains(picked);
        }

        internal static void Poll()
        {
            if (ShortcutPressed() && Enabled) Toggle();
        }

        private static void EnsureView()
        {
            if (document) return;

            panelSettings = Panels.CreateSettings(null, "DebugMenu (runtime)", 1000);
            // The menu ignores the player's UI scale: it has to stay usable whatever the game's UI looks like.
            Panels.ApplyScale(panelSettings, ScreenProfile.Current.Density);
            document = Panels.CreateDocument("[DebugMenu]", panelSettings);
            var root = document.rootVisualElement;
            var sheet = Panels.LoadStyleSheet("DebugMenu");
            if (sheet) root.styleSheets.Add(sheet);
            root.style.display = DisplayStyle.None;

            view = new DebugMenuView();
            root.Add(view);
            Panels.ApplyProfileClasses(view, ScreenProfile.Current);
            Panels.ApplyInputClasses(view, UIInput.Mode);
            ScreenProfile.Changed += OnProfileChanged;
            UIInput.ModeChanged += OnInputModeChanged;
        }

        private static void OnProfileChanged(ScreenState state)
        {
            if (!panelSettings || view == null) return;
            Panels.ApplyScale(panelSettings, state.Density);
            Panels.ApplyProfileClasses(view, state);
        }

        private static void OnInputModeChanged(InputMode mode)
        {
            if (view == null) return;
            Panels.ApplyInputClasses(view, mode);
            if (IsOpen && UIInput.IsNavigation) view.FocusFirst();
        }

        private static bool ShortcutPressed()
        {
            if (UIInput.IsTyping?.Invoke() == true) return false;
#if ENABLE_INPUT_SYSTEM && CORE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.backquoteKey.wasPressedThisFrame) return true;

            var pad = Gamepad.current;
            if (pad != null && (pad.leftStickButton.wasPressedThisFrame && pad.rightStickButton.isPressed ||
                                pad.rightStickButton.wasPressedThisFrame && pad.leftStickButton.isPressed))
                return true;

            var touchscreen = Touchscreen.current;
            if (touchscreen == null) return false;
            var pressed = 0;
            var began = false;
            foreach (var touch in touchscreen.touches)
            {
                if (touch.press.isPressed) pressed++;
                if (touch.press.wasPressedThisFrame) began = true;
            }

            return began && pressed == 3;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.BackQuote)) return true;
            if (Input.touchCount != 3) return false;
            for (var i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
            }

            return false;
#else
            return false;
#endif
        }

        internal static void ResetForTests()
        {
            if (document) UnityEngine.Object.DestroyImmediate(document.gameObject);
            ResetStatics();
        }

        // Domain reload is off in Enter Play Mode settings, so nothing static may survive into the next session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            entries.Clear();
            enabledOverride = null;
            document = null;
            panelSettings = null;
            view = null;
            IsOpen = false;
            OpenChanged = null;
        }
    }
}
