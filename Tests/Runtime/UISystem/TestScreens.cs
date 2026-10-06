using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Core.UISystem.Tests
{
    internal static class TestLog
    {
        public static readonly List<string> Entries = new();
    }

    /// <summary>Records its lifecycle; the view is built in code, so no settings or UXML are needed.</summary>
    internal abstract class LoggingScreen : UIScreen
    {
        public bool BackResult = true;
        public bool UseDefaultBack = true;

        private string Name => GetType().Name;

        protected override VisualElement CreateView()
        {
            var view = new VisualElement();
            view.Add(new Button { name = "first", text = "First" });
            view.Add(new Button { name = "second", text = "Second" });
            return view;
        }

        protected override void OnCreate() => TestLog.Entries.Add($"{Name}.Create");
        protected override void OnShow() => TestLog.Entries.Add($"{Name}.Show");
        protected override void OnHide() => TestLog.Entries.Add($"{Name}.Hide");
        protected override void OnFocus() => TestLog.Entries.Add($"{Name}.Focus");
        protected override void OnBlur() => TestLog.Entries.Add($"{Name}.Blur");
        protected override void OnDestroy() => TestLog.Entries.Add($"{Name}.Destroy");

        protected override bool OnBack()
        {
            TestLog.Entries.Add($"{Name}.Back");
            return UseDefaultBack ? base.OnBack() : BackResult;
        }
    }

    internal sealed class ScreenA : LoggingScreen
    { }

    internal sealed class ScreenB : LoggingScreen
    { }

    internal sealed class PopupP : LoggingScreen
    {
        public override ScreenKind Kind => ScreenKind.Popup;
    }

    internal sealed class OverlayO : LoggingScreen
    {
        public override ScreenKind Kind => ScreenKind.Overlay;
    }

    internal sealed class KeptK : LoggingScreen
    {
        public override bool KeepAlive => true;
    }

    internal sealed class ConfirmPopup : UIScreen<bool>
    {
        public override ScreenKind Kind => ScreenKind.Popup;

        protected override VisualElement CreateView() => new Button { name = "ok" };
    }

    internal sealed class FailingScreen : LoggingScreen
    {
        protected override void OnShow() => throw new System.InvalidOperationException("show failed");
    }

    /// <summary>No CreateView: its UXML comes from the registry (here: Screens.ViewResolver).</summary>
    internal sealed class UxmlScreen : UIScreen
    {
        protected override void OnCreate() => TestLog.Entries.Add("UxmlScreen.Create");
        protected override void OnDestroy() => TestLog.Entries.Add("UxmlScreen.Destroy");
    }
}
