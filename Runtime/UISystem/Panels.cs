using System;
using Core.Display;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Core.UISystem
{
    /// <summary>Panel setup shared by the screens and the debug menu.</summary>
    internal static class Panels
    {
        private const string Resources = "CoreUISystem/";

        private static readonly string[] sizeClasses = { "ui-size-compact", "ui-size-medium", "ui-size-expanded" };

        private static readonly string[] inputClasses =
            { "ui-input-touch", "ui-input-pointer", "ui-input-keyboard", "ui-input-gamepad" };

        internal static StyleSheet LoadStyleSheet(string name) =>
            UnityEngine.Resources.Load<StyleSheet>(Resources + name);

        /// <summary>A runtime copy: changing the scale must never touch the project's asset.</summary>
        internal static PanelSettings CreateSettings(PanelSettings source, string name, int sortingOrderOffset)
        {
            var settings = source ? Object.Instantiate(source) : ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = name;
            settings.hideFlags = HideFlags.DontSave;
            if (!settings.themeStyleSheet)
                settings.themeStyleSheet = UnityEngine.Resources.Load<ThemeStyleSheet>(Resources + "DefaultTheme");
            settings.sortingOrder += sortingOrderOffset;
            return settings;
        }

        internal static void ApplyScale(PanelSettings settings, float scale)
        {
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = scale;
        }

        internal static UIDocument CreateDocument(string name, PanelSettings settings)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            if (Application.isPlaying) Object.DontDestroyOnLoad(gameObject);
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = settings;
            gameObject.SetActive(true);
            document.rootVisualElement.pickingMode = PickingMode.Ignore;
            return document;
        }

        internal static VisualElement CreateLayer(VisualElement parent, string name)
        {
            var layer = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            layer.AddToClassList("ui-layer");
            layer.AddToClassList(name);
            parent.Add(layer);
            return layer;
        }

        internal static void ApplyProfileClasses(VisualElement root, in ScreenState state)
        {
            for (var i = 0; i < sizeClasses.Length; i++) root.EnableInClassList(sizeClasses[i], i == (int)state.SizeClass);
            root.EnableInClassList("ui-portrait", state.Orientation == Orientation.Portrait);
            root.EnableInClassList("ui-landscape", state.Orientation == Orientation.Landscape);
            root.EnableInClassList("ui-mobile", state.IsMobile);
            root.EnableInClassList("ui-desktop", !state.IsMobile);
        }

        internal static void ApplyInputClasses(VisualElement root, InputMode mode)
        {
            for (var i = 0; i < inputClasses.Length; i++) root.EnableInClassList(inputClasses[i], i == (int)mode);
            root.EnableInClassList("ui-input-navigation", mode is InputMode.Keyboard or InputMode.Gamepad);
        }

        internal static bool IsTextInput(VisualElement element)
        {
            for (; element != null; element = element.parent)
            {
                for (var type = element.GetType(); type != null; type = type.BaseType)
                {
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(TextInputBaseField<>)) return true;
                }
            }

            return false;
        }

        internal static float PanelUnitsPerPixel(IPanel panel)
        {
            if (panel == null || panel.contextType != ContextType.Player) return 1f;
            var origin = RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            var unit = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(100f, 0f));
            return Math.Abs(unit.x - origin.x) / 100f;
        }
    }
}
