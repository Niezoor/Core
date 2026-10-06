using System;
using System.Collections;
using System.Collections.Generic;
using Core.Utilities.Inspector;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.UISystem
{
    /// <summary>
    /// A runtime inspector: rows for any object (<see cref="InspectorModel"/>), kept in step with its current values.
    /// The debug menu is built on it; a game's own menu can be too (style it through the <c>ui-inspector</c> classes).
    /// </summary>
    [UxmlElement]
    public partial class InspectorView : VisualElement
    {
        public const string UssClassName = "ui-inspector";

        private readonly List<Action> refreshers = new();
        private readonly List<(VisualElement row, string label)> topRows = new();
        private readonly InspectorContext context;
        private IVisualElementScheduledItem refreshLoop;
        private object target;
        private string filter;

        public InspectorView() : this(null)
        { }

        public InspectorView(object target)
        {
            AddToClassList(UssClassName);
            context = new InspectorContext(o => ObjectSelected?.Invoke(o));
            RegisterCallback<AttachToPanelEvent>(_ => refreshLoop = schedule.Execute(Refresh).Every(RefreshIntervalMs));
            RegisterCallback<DetachFromPanelEvent>(_ => refreshLoop?.Pause());
            Target = target;
        }

        /// <summary>An object reference was clicked: open it (the debug menu pushes a page).</summary>
        public event Action<object> ObjectSelected;

        [UxmlAttribute]
        public long RefreshIntervalMs { get; set; } = 250;

        public object Target
        {
            get => target;
            set
            {
                target = value;
                Rebuild();
            }
        }

        /// <summary>Shows only the rows whose label contains the text.</summary>
        public string Filter
        {
            get => filter;
            set
            {
                filter = value;
                ApplyFilter();
            }
        }

        /// <summary>Reads every shown value again; done on a timer while the view is in a panel.</summary>
        public void Refresh()
        {
            foreach (var refresh in refreshers) refresh();
        }

        public void Rebuild()
        {
            Clear();
            refreshers.Clear();
            topRows.Clear();
            if (target == null)
            {
                var empty = new Label("Nothing to show.");
                empty.AddToClassList("ui-inspector__empty");
                Add(empty);
                return;
            }

            var members = InspectorModel.GetMembers(target);
            BuildRows(this, members, refreshers, topRows);
            if (members.Count == 0)
            {
                var empty = new Label($"{target.GetType().Name} has nothing to inspect.");
                empty.AddToClassList("ui-inspector__empty");
                Add(empty);
            }

            ApplyFilter();
        }

        private void BuildRows(VisualElement container, IReadOnlyList<InspectorMember> members, List<Action> refresh,
            List<(VisualElement, string)> rows = null)
        {
            foreach (var member in members)
            {
                AddDecorations(container, member);
                var row = CreateRow(member, out var rowRefresh);
                container.Add(row);
                if (rowRefresh != null) refresh.Add(rowRefresh);
                rows?.Add((row, member.Label));
            }
        }

        private static void AddDecorations(VisualElement container, InspectorMember member)
        {
            if (member.GetAttribute<SpaceAttribute>() is { } space)
            {
                var spacer = new VisualElement();
                spacer.style.height = space.height;
                container.Add(spacer);
            }

            if (member.GetAttribute<HeaderAttribute>() is { } header)
            {
                var label = new Label(header.header);
                label.AddToClassList("ui-inspector__header");
                container.Add(label);
            }
        }

        private VisualElement CreateRow(InspectorMember member, out Action refresh)
        {
            if (member.Kind == InspectorMemberKind.Button)
            {
                refresh = null;
                return InspectorControls.CreateButton(member, context);
            }

            var value = member.GetValue();
            var nested = InspectorModel.IsList(member.Type) || InspectorModel.IsNested(member.Type) ||
                         value != null && !(value is Exception) &&
                         (InspectorModel.IsList(value.GetType()) || InspectorModel.IsNested(value.GetType()));
            return nested ? CreateFoldout(member, out refresh) : InspectorControls.Create(member, context, out refresh);
        }

        private VisualElement CreateFoldout(InspectorMember member, out Action refresh)
        {
            var foldout = new Foldout { value = false };
            foldout.AddToClassList("ui-inspector__foldout");
            foldout.tooltip = member.Tooltip;
            var children = new List<Action>();
            object builtFor = null;
            var builtCount = -1;
            var built = false;

            void Build()
            {
                foldout.contentContainer.Clear();
                children.Clear();
                var value = member.GetValue();
                BuildRows(foldout.contentContainer, member.GetChildren(), children);
                builtFor = value;
                builtCount = Count(value);
                built = true;
            }

            void Update()
            {
                var value = member.GetValue();
                var count = Count(value);
                foldout.text = count >= 0 ? $"{member.Label} ({count})" : value == null ? $"{member.Label}: null" : member.Label;
                if (!foldout.value) return;
                // A replaced object or a list of another length needs new rows; otherwise the rows only re-read.
                if (!built || !SameValue(value, builtFor) || count != builtCount) Build();
                else foreach (var child in children) child();
            }

            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == foldout && evt.newValue) Update();
            });
            Update();
            refresh = Update;
            return foldout;
        }

        // A struct comes back as a new box on every read; only another type means other rows.
        private static bool SameValue(object a, object b) => a != null && a.GetType().IsValueType
            ? b != null && a.GetType() == b.GetType()
            : ReferenceEquals(a, b);

        private static int Count(object value) => value is IList list && InspectorModel.IsList(value.GetType())
            ? Mathf.Min(list.Count, InspectorMember.MaxElements)
            : -1;

        private void ApplyFilter()
        {
            foreach (var (row, label) in topRows)
            {
                var visible = string.IsNullOrEmpty(filter) ||
                              label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                row.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
