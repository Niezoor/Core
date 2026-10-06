using System;
using System.Collections.Generic;
using System.Linq;
using Core.Utilities.Inspector;
using Core.Utilities.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Core.UISystem
{
    /// <summary>The debug menu's panel: a page stack of folders and inspectors, with search.</summary>
    internal sealed class DebugMenuView : VisualElement
    {
        private sealed class Page
        {
            public string Title;
            public Func<VisualElement> Build;
        }

        private readonly List<Page> pages = new();
        private readonly List<(VisualElement item, string text)> items = new();
        private readonly Label title;
        private readonly Button backButton;
        private readonly TextField search;
        private readonly ScrollView content;
        private readonly List<InspectorView> inspectors = new();
        private readonly InspectorContext context;

        public DebugMenuView()
        {
            context = new InspectorContext(OpenObject);
            AddToClassList("dbg-root");
            pickingMode = PickingMode.Position;
            // A tap beside the panel closes the menu.
            RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == this) DebugMenu.Close();
            });

            var panel = new VisualElement();
            panel.AddToClassList("dbg-panel");
            Add(panel);

            var header = new VisualElement();
            header.AddToClassList("dbg-header");
            backButton = new Button(DebugMenu.Back) { text = "<" };
            backButton.AddToClassList("dbg-header__button");
            title = new Label();
            title.AddToClassList("dbg-header__title");
            var closeButton = new Button(DebugMenu.Close) { text = "X" };
            closeButton.AddToClassList("dbg-header__button");
            header.Add(backButton);
            header.Add(title);
            header.Add(closeButton);
            panel.Add(header);

            search = new TextField();
            search.AddToClassList("dbg-search");
            search.textEdition.placeholder = "Search";
            search.RegisterValueChangedCallback(evt => ApplySearch(evt.newValue));
            panel.Add(search);

            content = new ScrollView(ScrollViewMode.Vertical);
            content.AddToClassList("dbg-content");
            panel.Add(content);

            Push("Debug", () => Folder(string.Empty));
        }

        internal void OnOpened()
        {
            // Folders list live data (loaded settings, scenes): built again on every open.
            Show(pages[pages.Count - 1]);
        }

        internal void RefreshRoot()
        {
            if (pages.Count == 1) Show(pages[0]);
        }

        internal void OpenObject(object target)
        {
            switch (target)
            {
                case GameObject gameObject:
                    Push(gameObject.name, () => GameObjectPage(gameObject));
                    break;
                default:
                    Push(TitleOf(target), () => Inspect(target));
                    break;
            }
        }

        internal bool Back()
        {
            if (pages.Count <= 1) return false;
            pages.RemoveAt(pages.Count - 1);
            Show(pages[pages.Count - 1]);
            return true;
        }

        internal void FocusFirst()
        {
            schedule.Execute(() => content.Query<VisualElement>()
                .Where(e => e.focusable && e.canGrabFocus && e.tabIndex >= 0).First()?.Focus());
        }

        private void Push(string pageTitle, Func<VisualElement> build)
        {
            pages.Add(new Page { Title = pageTitle, Build = build });
            Show(pages[pages.Count - 1]);
        }

        private void Show(Page page)
        {
            content.Clear();
            items.Clear();
            inspectors.Clear();
            search.SetValueWithoutNotify(string.Empty);
            title.text = string.Join(" / ", pages.Select(p => p.Title));
            backButton.SetEnabled(pages.Count > 1);
            try
            {
                content.Add(page.Build());
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                content.Add(new Label($"This page failed: {exception.Message}"));
            }

            if (UIInput.IsNavigation) FocusFirst();
        }

        private void ApplySearch(string text)
        {
            foreach (var (item, itemText) in items)
            {
                var visible = string.IsNullOrEmpty(text) || itemText.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
                item.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            foreach (var inspector in inspectors) inspector.Filter = text;
        }

        private VisualElement Folder(string prefix)
        {
            var list = new VisualElement();
            var seen = new HashSet<string>();
            foreach (var (path, open) in Pages())
            {
                if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var rest = path.Substring(prefix.Length);
                var slash = rest.IndexOf('/');
                var name = slash < 0 ? rest : rest.Substring(0, slash);
                if (!seen.Add(name)) continue;

                if (slash < 0)
                {
                    var page = open;
                    list.Add(Item(name, true, () => Push(name, page)));
                }
                else
                {
                    var folder = prefix + name + "/";
                    list.Add(Item(name + "/", true, () => Push(name, () => Folder(folder))));
                }
            }

            if (seen.Count == 0) list.Add(new Label("Empty."));
            return list;
        }

        private IEnumerable<(string path, Func<VisualElement> page)> Pages()
        {
            yield return ("Screen", () => Inspect(new ScreenDebug()));
            yield return ("Time", () => Inspect(new TimeDebug()));
            yield return ("UI Screens", () => Inspect(new StackDebug()));
            yield return ("Scene Hierarchy", ScenesPage);
            foreach (var asset in SettingsRegistry.Loaded.OrderBy(a => a.GetType().Name))
            {
                var settings = asset;
                yield return ($"Settings/{settings.GetType().Name}", () => Inspect(settings));
            }

            foreach (var (path, target) in DebugMenu.Entries.OrderBy(e => e.path, StringComparer.Ordinal))
            {
                var getTarget = target;
                yield return (path, () => Inspect(getTarget()));
            }
        }

        private VisualElement Inspect(object target)
        {
            var page = new VisualElement();
            if (target is Behaviour behaviour)
            {
                page.Add(InspectorControls.Create(InspectorMember.Custom("Enabled", typeof(bool),
                    () => behaviour && behaviour.enabled, v => behaviour.enabled = (bool)v), context, out _));
            }

            var inspector = new InspectorView(target);
            inspector.ObjectSelected += OpenObject;
            inspectors.Add(inspector);
            page.Add(inspector);
            return page;
        }

        private VisualElement ScenesPage()
        {
            var list = new VisualElement();
            var scenes = new List<Scene>();
            for (var i = 0; i < SceneManager.sceneCount; i++) scenes.Add(SceneManager.GetSceneAt(i));
            var persistent = DebugMenu.PersistentScene;
            if (persistent.IsValid()) scenes.Add(persistent);

            foreach (var scene in scenes)
            {
                if (!scene.isLoaded) continue;
                list.Add(Header(scene.name));
                foreach (var root in scene.GetRootGameObjects())
                {
                    var gameObject = root;
                    list.Add(Item(gameObject.name, true, () => OpenObject(gameObject)));
                }
            }

            return list;
        }

        private VisualElement GameObjectPage(GameObject gameObject)
        {
            var page = new VisualElement();
            if (!gameObject)
            {
                page.Add(new Label("Destroyed."));
                return page;
            }

            var transform = gameObject.transform;
            page.Add(InspectorControls.Create(InspectorMember.Custom("Active", typeof(bool),
                () => gameObject && gameObject.activeSelf, v => gameObject.SetActive((bool)v)), context, out var active));
            page.Add(InspectorControls.Create(InspectorMember.Custom("Position", typeof(Vector3),
                () => transform ? transform.localPosition : Vector3.zero, v => transform.localPosition = (Vector3)v), context,
                out var position));
            page.Add(InspectorControls.Create(InspectorMember.Custom("Rotation", typeof(Vector3),
                () => transform ? transform.localEulerAngles : Vector3.zero, v => transform.localEulerAngles = (Vector3)v),
                context, out var rotation));
            page.Add(InspectorControls.Create(InspectorMember.Custom("Scale", typeof(Vector3),
                () => transform ? transform.localScale : Vector3.one, v => transform.localScale = (Vector3)v), context,
                out var scale));
            page.schedule.Execute(() =>
            {
                active();
                position();
                rotation();
                scale();
            }).Every(250);

            page.Add(Header("Components"));
            foreach (var component in gameObject.GetComponents<Component>())
            {
                if (!component || component is Transform) continue;
                var target = component;
                page.Add(Item(target.GetType().Name, true, () => OpenObject(target)));
            }

            if (transform.childCount > 0) page.Add(Header("Children"));
            foreach (Transform child in transform)
            {
                var target = child.gameObject;
                page.Add(Item(target.name, true, () => OpenObject(target)));
            }

            return page;
        }

        private VisualElement Item(string text, bool opens, Action onClick)
        {
            var button = new Button(onClick) { text = opens ? $"{text}  >" : text };
            button.AddToClassList("dbg-item");
            items.Add((button, text));
            return button;
        }

        private static Label Header(string text)
        {
            var label = new Label(text);
            label.AddToClassList("dbg-section");
            return label;
        }

        private static string TitleOf(object target) => target switch
        {
            null => "null",
            Object unityObject when unityObject => unityObject is Component component
                ? $"{component.gameObject.name}.{component.GetType().Name}"
                : unityObject.name,
            _ => target.GetType().Name,
        };
    }
}
