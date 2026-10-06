using System;
using System.Collections.Generic;
using Core.Display;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.UISystem
{
    /// <summary>
    /// The screen stack. <see cref="Push{T}"/> opens a screen on top; back (Esc, Android back, gamepad B) and
    /// <see cref="UIScreen.Close"/> take it off. Lifecycle callbacks run at once; show/hide transitions play after.
    /// One UI Toolkit panel, created on first use and kept across scenes.
    /// </summary>
    public static class Screens
    {
        private static readonly List<UIScreen> stack = new();
        private static readonly List<UIScreen> overlays = new();
        private static readonly Dictionary<Type, UIScreen> kept = new();
        private static ScreenHost host;
        private static UIScreen focusedTop;

        /// <summary>Picks the UXML of a screen; tests replace it. Default: <see cref="UISystemSettings"/>.</summary>
        internal static Func<UIScreen, ScreenState, VisualTreeAsset> ViewResolver;

        public static event Action<UIScreen> Opened;
        public static event Action<UIScreen> Closed;

        /// <summary>The top of the stack changed (null: the stack is empty).</summary>
        public static event Action<UIScreen> TopChanged;

        /// <summary>Back was pressed and nothing used it, e.g. on the main menu: ask to quit, or ignore.</summary>
        public static event Action BackUnhandled;

        /// <summary>Screens and popups, bottom to top.</summary>
        public static IReadOnlyList<UIScreen> Stack => stack;

        public static IReadOnlyList<UIScreen> Overlays => overlays;

        public static UIScreen Top => stack.Count > 0 ? stack[stack.Count - 1] : null;

        /// <summary>The panel's root; carries the profile and input classes (<c>ui-size-compact</c>, ...).</summary>
        public static VisualElement RootElement => Host.Root;

        public static IPanel Panel => Host.Root.panel;

        internal static bool HasHost => host;

        private static ScreenHost Host
        {
            get
            {
                if (!host) host = ScreenHost.Create();
                return host;
            }
        }

        /// <summary>Opens a screen on top; <paramref name="setup"/> runs before its view is built, for arguments.</summary>
        public static T Push<T>(Action<T> setup = null) where T : UIScreen, new() =>
            (T)Open(typeof(T), setup == null ? null : s => setup((T)s), () => new T());

        public static UIScreen Push(Type type, Action<UIScreen> setup = null)
        {
            if (type == null || type.IsAbstract || !typeof(UIScreen).IsAssignableFrom(type))
                throw new ArgumentException($"{type?.Name ?? "null"} is not a concrete UIScreen.", nameof(type));
            return Open(type, setup, () => (UIScreen)Activator.CreateInstance(type));
        }

        /// <summary>Opens a screen in place of the top one.</summary>
        public static T Replace<T>(Action<T> setup = null) where T : UIScreen, new()
        {
            var top = Top;
            // Pushed first, so the screen below never shows between the two.
            var screen = Push(setup);
            if (top != null && top != screen) Close(top);
            return screen;
        }

        public static void Close(UIScreen screen)
        {
            if (screen == null || !screen.IsOpen) return;

            if (!stack.Remove(screen)) overlays.Remove(screen);
            screen.IsOpen = false;
            if (screen.IsFocused)
            {
                screen.IsFocused = false;
                Call(screen, screen.InvokeBlur);
            }

            if (screen.IsVisible) Hide(screen, !screen.KeepAlive);
            else if (!screen.KeepAlive) Destroy(screen);

            screen.Closed();
            Closed?.Invoke(screen);
            UpdatePresentation();
        }

        /// <summary>Closes every screen above <paramref name="screen"/>.</summary>
        public static void CloseAbove(UIScreen screen)
        {
            var index = stack.IndexOf(screen);
            if (index < 0) return;
            while (stack.Count > index + 1) Close(Top);
        }

        /// <summary>Closes everything at once, without transitions (e.g. before a scene change). Overlays too.</summary>
        public static void Clear()
        {
            var all = new List<UIScreen>(stack);
            all.AddRange(overlays);
            stack.Clear();
            overlays.Clear();
            for (var i = all.Count - 1; i >= 0; i--)
            {
                var screen = all[i];
                screen.IsOpen = false;
                if (screen.IsFocused)
                {
                    screen.IsFocused = false;
                    Call(screen, screen.InvokeBlur);
                }

                if (screen.IsVisible)
                {
                    screen.IsVisible = false;
                    screen.visibilityVersion++;
                    Call(screen, screen.InvokeHide);
                    if (screen.Root != null) screen.Root.style.display = DisplayStyle.None;
                }

                if (!screen.KeepAlive) Destroy(screen);
                screen.Closed();
                Closed?.Invoke(screen);
            }

            UpdatePresentation();
        }

        /// <summary>What back does: asks the top screen (<see cref="UIScreen.OnBack"/>).</summary>
        /// <returns>False when nothing used it (<see cref="BackUnhandled"/> was raised).</returns>
        public static bool Back()
        {
            var top = Top;
            var handled = false;
            if (top != null)
            {
                try
                {
                    handled = top.InvokeBack();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    handled = true;
                }
            }

            if (!handled) BackUnhandled?.Invoke();
            return handled;
        }

        /// <summary>The topmost open screen of the type (stack or overlays), or null.</summary>
        public static T Get<T>() where T : UIScreen
        {
            for (var i = stack.Count - 1; i >= 0; i--)
            {
                if (stack[i] is T screen) return screen;
            }

            foreach (var overlay in overlays)
            {
                if (overlay is T screen) return screen;
            }

            return null;
        }

        public static bool IsOpen<T>() where T : UIScreen => Get<T>() != null;

        /// <summary>
        /// Whether the UI takes a pointer at this screen position (origin bottom-left, like <c>Input.mousePosition</c>),
        /// so the game should not.
        /// </summary>
        public static bool IsPointerOverUI(Vector2 screenPosition)
        {
            if (DebugMenu.IsPointerOver(screenPosition)) return true;
            if (!host || host.Root.panel == null) return false;
            var panel = host.Root.panel;
            var point = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            var picks = new List<VisualElement>();
            panel.PickAll(point, picks);
            foreach (var picked in picks)
            {
                // The panel's own root elements pick too, and so does a screen fading out: neither counts.
                if (!host.Root.Contains(picked)) continue;
                var owner = OwnerOf(picked);
                if (owner == null || owner.IsVisible) return true;
            }

            return false;
        }

        private static UIScreen OwnerOf(VisualElement element)
        {
            for (; element != null; element = element.parent)
            {
                if (element.userData is UIScreen screen && element == screen.Root) return screen;
            }

            return null;
        }

        private static UIScreen Open(Type type, Action<UIScreen> setup, Func<UIScreen> create)
        {
            var root = Host;
            if (!kept.TryGetValue(type, out var screen)) screen = create();
            else if (screen.IsOpen) Close(screen);

            setup?.Invoke(screen);
            screen.Opening();
            if (!screen.isBuilt) Build(screen);

            screen.IsOpen = true;
            if (screen.Kind == ScreenKind.Overlay)
            {
                overlays.Add(screen);
                root.OverlayLayer.Add(screen.Root);
            }
            else
            {
                stack.Add(screen);
                root.StackLayer.Add(screen.Root);
            }

            if (screen.KeepAlive) kept[type] = screen;
            Opened?.Invoke(screen);
            UpdatePresentation();
            return screen;
        }

        private static void UpdatePresentation()
        {
            var top = Top;
            if (focusedTop != null && focusedTop != top)
            {
                if (focusedTop.IsFocused)
                {
                    focusedTop.IsFocused = false;
                    Call(focusedTop, focusedTop.InvokeBlur);
                }
            }

            // Visible: the top and everything below it down to, and including, the first full screen.
            var lowestVisible = 0;
            for (var i = stack.Count - 1; i >= 0; i--)
            {
                if (stack[i].Kind != ScreenKind.Screen) continue;
                lowestVisible = i;
                break;
            }

            for (var i = 0; i < stack.Count; i++)
            {
                var screen = stack[i];
                var visible = i >= lowestVisible;
                if (visible && !screen.IsVisible) Show(screen);
                else if (!visible && screen.IsVisible) Hide(screen, false);
                screen.Root.EnableInClassList("ui-screen--covered", i < stack.Count - 1);
            }

            foreach (var overlay in overlays)
            {
                if (!overlay.IsVisible) Show(overlay);
            }

            if (top != null && !top.IsFocused)
            {
                top.IsFocused = true;
                Call(top, top.InvokeFocus);
                host.RequestFocus(top);
            }

            if (focusedTop != top)
            {
                focusedTop = top;
                TopChanged?.Invoke(top);
            }
        }

        private static void Build(UIScreen screen)
        {
            var root = new VisualElement { name = screen.GetType().Name };
            root.AddToClassList(UIScreen.ScreenClass);
            root.AddToClassList($"{UIScreen.ScreenClass}--{screen.Kind.ToString().ToLowerInvariant()}");
            // Only a popup's scrim blocks what is below; a screen's empty areas let clicks through to the game.
            root.pickingMode = screen.Kind == ScreenKind.Popup ? PickingMode.Position : PickingMode.Ignore;
            root.style.display = DisplayStyle.None;
            root.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == root && screen.CloseOnScrimClick && screen == Top) screen.Close();
            });
            root.RegisterCallback<FocusInEvent>(evt => OnFocusIn(screen, evt), TrickleDown.TrickleDown);
            // A screen fading out is still drawn but must not be clicked any more.
            root.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (!screen.IsVisible) evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            root.userData = screen;

            screen.Root = root;
            BuildView(screen, ScreenProfile.Current);
            screen.isBuilt = true;
            Call(screen, screen.InvokeCreate);
        }

        private static void BuildView(UIScreen screen, in ScreenState state)
        {
            var view = screen.InvokeCreateView();
            if (view == null)
            {
                var asset = ResolveView(screen, state);
                if (!asset)
                {
                    var name = screen.GetType().Name;
                    throw new InvalidOperationException(
                        $"{name} has no view: create {name}.uxml (the editor pairs it by name) or assign one in " +
                        "Project Settings > Core > UI System, or override CreateView.");
                }

                view = asset.Instantiate();
                screen.viewAsset = asset;
            }
            else
            {
                screen.viewAsset = null;
            }

            view.AddToClassList(UIScreen.ViewClass);
            view.pickingMode = PickingMode.Ignore;
            screen.view = view;
            screen.Root.Add(view);
        }

        private static VisualTreeAsset ResolveView(UIScreen screen, in ScreenState state)
        {
            if (ViewResolver != null) return ViewResolver(screen, state);
            return UISystemSettings.TryGet(out var settings) ? settings.FindEntry(screen.GetType())?.GetView(state) : null;
        }

        private static void Show(UIScreen screen)
        {
            screen.IsVisible = true;
            screen.Root.style.display = DisplayStyle.Flex;
            screen.Root.pickingMode = screen.Kind == ScreenKind.Popup ? PickingMode.Position : PickingMode.Ignore;
            var version = ++screen.visibilityVersion;
            Call(screen, screen.InvokeShow);
            Animate(screen, true, version);
        }

        private static void Hide(UIScreen screen, bool destroy)
        {
            screen.IsVisible = false;
            screen.Root.pickingMode = PickingMode.Ignore;
            var version = ++screen.visibilityVersion;
            Call(screen, screen.InvokeHide);
            Animate(screen, false, version, destroy);
        }

        private static async void Animate(UIScreen screen, bool show, int version, bool destroy = false)
        {
            try
            {
                await screen.InvokeAnimateAsync(show, host ? host.destroyCancellationToken : default);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            // Shown or hidden again meanwhile: the newer transition decides.
            if (show || version != screen.visibilityVersion || screen.Root == null) return;
            screen.Root.style.display = DisplayStyle.None;
            if (destroy && !screen.IsOpen) Destroy(screen);
        }

        private static void Destroy(UIScreen screen)
        {
            if (!screen.isBuilt) return;
            Call(screen, screen.InvokeDestroy);
            screen.Root.RemoveFromHierarchy();
            screen.Root = null;
            screen.view = null;
            screen.viewAsset = null;
            screen.lastFocused = null;
            screen.isBuilt = false;
            if (kept.TryGetValue(screen.GetType(), out var keptScreen) && keptScreen == screen) kept.Remove(screen.GetType());
        }

        /// <summary>Swaps the views whose layout variant no longer matches the screen.</summary>
        internal static void OnProfileClassChanged(ScreenState state)
        {
            var built = new List<UIScreen>(stack);
            built.AddRange(overlays);
            foreach (var screen in kept.Values)
            {
                if (!built.Contains(screen)) built.Add(screen);
            }

            foreach (var screen in built)
            {
                if (!screen.isBuilt || screen.viewAsset == null) continue;
                var asset = ResolveView(screen, state);
                if (!asset || asset == screen.viewAsset) continue;
                RebuildView(screen, state);
            }
        }

        private static void RebuildView(UIScreen screen, in ScreenState state)
        {
            var focused = screen.IsFocused;
            var visible = screen.IsVisible;
            if (focused) Call(screen, screen.InvokeBlur);
            if (visible) Call(screen, screen.InvokeHide);
            Call(screen, screen.InvokeDestroy);

            screen.view.RemoveFromHierarchy();
            screen.lastFocused = null;
            BuildView(screen, state);
            Call(screen, screen.InvokeCreate);

            if (visible) Call(screen, screen.InvokeShow);
            if (!focused) return;
            Call(screen, screen.InvokeFocus);
            host.RequestFocus(screen);
        }

        private static void OnFocusIn(UIScreen screen, FocusInEvent evt)
        {
            if (screen.Kind == ScreenKind.Overlay) return;
            if (screen == Top)
            {
                screen.lastFocused = evt.target as VisualElement;
                return;
            }

            // Keyboard/gamepad navigation wandered into a covered screen: back to the top.
            host.RequestFocus(Top, true);
        }

        /// <summary>Moves focus into the top screen, as keyboard/gamepad navigation needs.</summary>
        internal static void FocusTop(UIScreen screen, bool force)
        {
            if (screen == null || !screen.IsFocused || screen.Root?.panel == null) return;
            if (!force && !UIInput.IsNavigation) return;

            var current = screen.Root.panel.focusController.focusedElement as VisualElement;
            var target = screen.lastFocused is { panel: not null } last && last.canGrabFocus &&
                         screen.Root.Contains(last)
                ? last
                : screen.DefaultFocus ?? FirstFocusable(screen.Root);

            if (target != null) target.Focus();
            else if (current != null && !screen.Root.Contains(current)) current.Blur();
        }

        private static VisualElement FirstFocusable(VisualElement root) =>
            root.Query<VisualElement>().Where(e => e != root && e.focusable && e.canGrabFocus && e.tabIndex >= 0).First();

        private static void Call(UIScreen screen, Action callback)
        {
            try
            {
                callback();
            }
            catch (Exception exception)
            {
                // A broken screen must not leave the stack half-updated.
                Debug.LogException(exception);
            }
        }

        internal static void ResetForTests()
        {
            if (host) UnityEngine.Object.DestroyImmediate(host.gameObject);
            ResetStatics();
        }

        // Domain reload is off in Enter Play Mode settings, so nothing static may survive into the next session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            stack.Clear();
            overlays.Clear();
            kept.Clear();
            host = null;
            focusedTop = null;
            ViewResolver = null;
            Opened = null;
            Closed = null;
            TopChanged = null;
            BackUnhandled = null;
        }
    }
}
