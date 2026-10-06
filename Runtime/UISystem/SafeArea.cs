using System;
using Core.Display;
using UnityEngine.UIElements;

namespace Core.UISystem
{
    [Flags]
    public enum SafeAreaEdges
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8,
        All = Left | Right | Top | Bottom,
    }

    /// <summary>
    /// Pads its content away from notches, rounded corners and system bars (<see cref="ScreenState.SafeInsets"/>).
    /// UI Toolkit has no safe area of its own; put this around a screen's content in UXML.
    /// </summary>
    [UxmlElement]
    public partial class SafeArea : VisualElement
    {
        public const string UssClassName = "ui-safe-area";

        private SafeAreaEdges edges = SafeAreaEdges.All;

        public SafeArea()
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ScreenProfile.Changed += OnProfileChanged;
                Apply();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => ScreenProfile.Changed -= OnProfileChanged);
        }

        [UxmlAttribute]
        public SafeAreaEdges Edges
        {
            get => edges;
            set
            {
                edges = value;
                Apply();
            }
        }

        private void OnProfileChanged(ScreenState _)
        {
            Apply();
            // The panel scale is applied by the same event and takes effect a frame later; measure again then.
            schedule.Execute(Apply);
        }

        private void Apply()
        {
            if (panel == null || panel.contextType != ContextType.Player)
            {
                style.paddingLeft = style.paddingRight = style.paddingTop = style.paddingBottom = StyleKeyword.Null;
                return;
            }

            var insets = ScreenProfile.Current.SafeInsets.Scaled(Panels.PanelUnitsPerPixel(panel));
            style.paddingLeft = (edges & SafeAreaEdges.Left) != 0 ? insets.Left : 0f;
            style.paddingRight = (edges & SafeAreaEdges.Right) != 0 ? insets.Right : 0f;
            style.paddingTop = (edges & SafeAreaEdges.Top) != 0 ? insets.Top : 0f;
            style.paddingBottom = (edges & SafeAreaEdges.Bottom) != 0 ? insets.Bottom : 0f;
        }
    }
}
