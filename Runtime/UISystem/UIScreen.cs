using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.UISystem
{
    public enum ScreenKind
    {
        /// <summary>Fills the screen and hides the screens below it.</summary>
        Screen,

        /// <summary>Over the screens below, which stay visible; a scrim blocks them.</summary>
        Popup,

        /// <summary>Outside the stack and above it: no focus, no back, clicks pass through (toasts, banners).</summary>
        Overlay,
    }

    /// <summary>
    /// A screen: this class is its logic, a UXML file its look (registered in Project Settings › Core › UI System, or
    /// built in code by overriding <see cref="CreateView"/>). Opened with <see cref="Screens.Push{T}"/>.
    /// </summary>
    public abstract class UIScreen
    {
        internal const string ScreenClass = "ui-screen";
        internal const string ViewClass = "ui-screen__view";

        internal VisualElement view;
        internal VisualTreeAsset viewAsset;
        internal VisualElement lastFocused;
        internal int visibilityVersion;
        internal bool isBuilt;

        public virtual ScreenKind Kind => ScreenKind.Screen;

        /// <summary>Kept with its view after closing and reused by the next push of its type.</summary>
        public virtual bool KeepAlive => false;

        /// <summary>A popup closes when the player taps outside it.</summary>
        public virtual bool CloseOnScrimClick => false;

        /// <summary>Focused first in keyboard/gamepad mode; null = the first element that can take focus.</summary>
        public virtual VisualElement DefaultFocus => null;

        /// <summary>The screen's element in the panel; the view is inside it.</summary>
        public VisualElement Root { get; internal set; }

        /// <summary>In the stack (or shown as an overlay), whether visible right now or not.</summary>
        public bool IsOpen { get; internal set; }

        public bool IsVisible { get; internal set; }

        /// <summary>On top of the stack: gets focus and back.</summary>
        public bool IsFocused { get; internal set; }

        public void Close() => Screens.Close(this);

        /// <summary>The named element of the view; throws a clear error when the UXML has none.</summary>
        protected T Q<T>(string name = null, string className = null) where T : VisualElement
        {
            if (Root == null) throw new InvalidOperationException($"{GetType().Name} has no view yet - query it in OnCreate.");
            return Root.Q<T>(name, className) ?? throw new InvalidOperationException(
                $"{GetType().Name}: the view has no {typeof(T).Name}" +
                (name != null ? $" named \"{name}\"" : string.Empty) +
                (className != null ? $" with class \"{className}\"" : string.Empty) + ".");
        }

        /// <summary>A view built in code; null (default) instantiates the registered UXML.</summary>
        protected virtual VisualElement CreateView() => null;

        /// <summary>Once per view, after it is built (again after a layout variant is swapped).</summary>
        protected virtual void OnCreate()
        { }

        /// <summary>Every time the screen becomes visible.</summary>
        protected virtual void OnShow()
        { }

        protected virtual void OnHide()
        { }

        /// <summary>Became the top of the stack.</summary>
        protected virtual void OnFocus()
        { }

        /// <summary>Another screen or popup opened above it, or it is closing.</summary>
        protected virtual void OnBlur()
        { }

        /// <summary>The view is torn down: closed (and hidden), or a layout variant is about to replace it.</summary>
        protected virtual void OnDestroy()
        { }

        /// <summary>
        /// Esc, Android back or gamepad B while this is the top. The default closes it unless it is the last screen.
        /// </summary>
        /// <returns>False when back was not used, so <see cref="Screens.BackUnhandled"/> is raised.</returns>
        protected virtual bool OnBack()
        {
            if (Screens.Stack.Count <= 1) return false;
            Close();
            return true;
        }

        /// <summary>
        /// Plays the show or hide transition. The default toggles <c>ui-screen--hidden</c> and waits for the USS
        /// transitions it starts; override for a transition in code.
        /// </summary>
        protected virtual Awaitable AnimateAsync(bool show, CancellationToken cancellationToken) =>
            ScreenTransitions.ToggleHiddenClassAsync(Root, show, cancellationToken);

        // Screens calls the lifecycle through these: game screens override the protected methods from their own
        // assemblies, where 'protected internal' would demand a different modifier on every override.
        internal VisualElement InvokeCreateView() => CreateView();
        internal void InvokeCreate() => OnCreate();
        internal void InvokeShow() => OnShow();
        internal void InvokeHide() => OnHide();
        internal void InvokeFocus() => OnFocus();
        internal void InvokeBlur() => OnBlur();
        internal void InvokeDestroy() => OnDestroy();
        internal bool InvokeBack() => OnBack();
        internal Awaitable InvokeAnimateAsync(bool show, CancellationToken token) => AnimateAsync(show, token);

        internal virtual void Opening()
        { }

        internal virtual void Closed()
        { }
    }

    /// <summary>A screen that gives back a result, e.g. a confirmation popup.</summary>
    public abstract class UIScreen<TResult> : UIScreen
    {
        private readonly List<AwaitableCompletionSource<TResult>> waiting = new();
        private TResult result;
        private bool closed;

        /// <summary>Closes the screen with a result for <see cref="WaitForResultAsync"/>.</summary>
        public void Close(TResult value)
        {
            result = value;
            Close();
        }

        /// <summary>
        /// The result once the screen is closed; <c>default</c> when it was closed without one (back, cleared stack).
        /// Every caller gets its own awaitable, so several may wait.
        /// </summary>
        public Awaitable<TResult> WaitForResultAsync(CancellationToken cancellationToken = default)
        {
            var source = new AwaitableCompletionSource<TResult>();
            if (closed)
            {
                source.SetResult(result);
                return source.Awaitable;
            }

            waiting.Add(source);
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    if (waiting.Remove(source)) source.TrySetCanceled();
                });
            }

            return source.Awaitable;
        }

        internal override void Closed()
        {
            closed = true;
            var sources = waiting.ToArray();
            waiting.Clear();
            foreach (var source in sources) source.TrySetResult(result);
        }

        internal override void Opening()
        {
            closed = false;
            result = default;
        }
    }
}
