using Core.Bootstrap;
using Core.Display;
using Core.Utilities.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.UISystem
{
    /// <summary>The panel of <see cref="Screens"/>. Created by it alone, never added by hand.</summary>
    [AddComponentMenu("")]
    internal sealed class ScreenHost : MonoBehaviour
    {
        private UIDocument document;
        private PanelSettings panelSettings;
        private bool scaleWithProfile;
        private UIScreen pendingFocus;
        private bool pendingForce;

        internal VisualElement Root { get; private set; }
        internal VisualElement StackLayer { get; private set; }
        internal VisualElement OverlayLayer { get; private set; }

        internal static ScreenHost Create()
        {
            if (!UISystemSettings.TryGet(out var settings) && !Application.isEditor && !SettingsRegistry.IsLoaded)
            {
                Debug.LogWarning("[UISystem] Screens opened before the settings were loaded: the panel uses the default " +
                                 "theme. Open screens after the splash (LoadSettingsTask), or mark UISystemSettings " +
                                 "[PreloadedSettings].");
            }

            var panel = Panels.CreateSettings(settings ? settings.PanelSettings : null, "UISystem (runtime)", 0);
            var scale = !settings || settings.ScaleWithProfile;
            if (scale) Panels.ApplyScale(panel, ScreenProfile.Current.Scale);

            var document = Panels.CreateDocument("[UISystem]", panel);
            var host = document.gameObject.AddComponent<ScreenHost>();
            host.document = document;
            host.panelSettings = panel;
            host.scaleWithProfile = scale;
            host.BuildRoot(settings);
            return host;
        }

        internal void RequestFocus(UIScreen screen, bool force = false)
        {
            pendingFocus = screen;
            pendingForce |= force;
        }

        private void BuildRoot(UISystemSettings settings)
        {
            var documentRoot = document.rootVisualElement;
            var baseSheet = Panels.LoadStyleSheet("UISystem");
            if (baseSheet) documentRoot.styleSheets.Add(baseSheet);
            if (settings)
            {
                foreach (var sheet in settings.StyleSheets)
                {
                    if (sheet) documentRoot.styleSheets.Add(sheet);
                }
            }

            Root = new VisualElement { name = "ui-root", pickingMode = PickingMode.Ignore };
            Root.AddToClassList("ui-root");
            documentRoot.Add(Root);
            StackLayer = Panels.CreateLayer(Root, "ui-layer--stack");
            OverlayLayer = Panels.CreateLayer(Root, "ui-layer--overlays");

            Panels.ApplyProfileClasses(Root, ScreenProfile.Current);
            Panels.ApplyInputClasses(Root, UIInput.Mode);
            ScreenProfile.ClassChanged += OnClassChanged;
            ScreenProfile.Changed += OnProfileChanged;
            UIInput.ModeChanged += OnInputModeChanged;
            Boot.Restarting += Screens.Clear;
        }

        private void Update()
        {
            if (pendingFocus == null) return;
            // A frame later than requested: an element shown this frame can take focus only once styles resolved.
            var screen = pendingFocus;
            var force = pendingForce;
            pendingFocus = null;
            pendingForce = false;
            Screens.FocusTop(screen, force);
        }

        private void OnClassChanged(ScreenState state)
        {
            Panels.ApplyProfileClasses(Root, state);
            Screens.OnProfileClassChanged(state);
        }

        private void OnProfileChanged(ScreenState state)
        {
            if (scaleWithProfile) Panels.ApplyScale(panelSettings, state.Scale);
        }

        private void OnInputModeChanged(InputMode mode)
        {
            Panels.ApplyInputClasses(Root, mode);
            var top = Screens.Top;
            if (top == null || !UIInput.IsNavigation) return;
            var focused = Root.panel?.focusController.focusedElement as VisualElement;
            if (focused == null || top.Root == null || !top.Root.Contains(focused)) RequestFocus(top);
        }

        private void OnDestroy()
        {
            ScreenProfile.ClassChanged -= OnClassChanged;
            ScreenProfile.Changed -= OnProfileChanged;
            UIInput.ModeChanged -= OnInputModeChanged;
            Boot.Restarting -= Screens.Clear;
            if (panelSettings) Destroy(panelSettings);
        }
    }
}
