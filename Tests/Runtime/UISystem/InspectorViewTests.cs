using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Display;
using Core.Utilities.Inspector;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Core.UISystem.Tests
{
    public class InspectorViewTests
    {
        private enum Mode
        {
            Easy,
            Hard,
        }

#pragma warning disable CS0649
        private sealed class Target
        {
            public int level = 2;
            [UnityEngine.Range(0f, 1f)] public float volume = 0.5f;
            public string title = "hi";
            public bool enabled;
            public Mode mode;
            public Vector3 position;
            public List<int> numbers = new() { 1, 2 };
            public int clicks;

            [Button] private void Click() => clicks++;
        }
#pragma warning restore CS0649

        [SetUp]
        public void SetUp()
        {
            ScreenProfile.ResetForTests();
            UIInput.ResetForTests();
            DebugMenu.ResetForTests();
            Screens.ResetForTests();
        }

        [TearDown]
        public void TearDown() => SetUp();

        private static T Field<T>(VisualElement view, string label) where T : VisualElement =>
            view.Query<T>().ToList().FirstOrDefault(f => f.Q<Label>(className: "unity-base-field__label")?.text == label);

        private static void Click(Button button)
        {
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(EventBase) }, null);
            using var click = ClickEvent.GetPooled();
            invoke.Invoke(button.clickable, new object[] { click });
        }

        [Test]
        public void BuildsAControlPerValueAndAButtonPerMethod()
        {
            var view = new InspectorView(new Target());

            Assert.IsNotNull(Field<IntegerField>(view, "Level"));
            Assert.IsNotNull(Field<Slider>(view, "Volume"));
            Assert.IsNotNull(Field<TextField>(view, "Title"));
            Assert.IsNotNull(Field<Toggle>(view, "Enabled"));
            Assert.IsNotNull(Field<EnumField>(view, "Mode"));
            Assert.IsNotNull(Field<Vector3Field>(view, "Position"));
            Assert.IsNotNull(view.Query<Foldout>().Where(f => f.text.StartsWith("Numbers")).First());
            Assert.IsNotNull(view.Query<Button>().Where(b => b.text == "Click").First());
        }

        [Test]
        public void EditingAControlWritesTheObject()
        {
            var target = new Target();
            var view = new InspectorView(target);
            // Change events are dispatched only inside a panel.
            Screens.RootElement.Add(view);

            Field<IntegerField>(view, "Level").value = 7;
            Field<Toggle>(view, "Enabled").value = true;

            Assert.AreEqual(7, target.level);
            Assert.IsTrue(target.enabled);
        }

        [Test]
        public void RefreshShowsChangesMadeElsewhere()
        {
            var target = new Target();
            var view = new InspectorView(target);

            target.level = 99;
            view.Refresh();

            Assert.AreEqual(99, Field<IntegerField>(view, "Level").value);
        }

        [Test]
        public void ButtonCallsTheMethod()
        {
            var target = new Target();
            var view = new InspectorView(target);
            var button = view.Query<Button>().Where(b => b.text == "Click").First();

            Click(button);

            Assert.AreEqual(1, target.clicks);
        }

        [Test]
        public void FilterHidesOtherRows()
        {
            var view = new InspectorView(new Target()) { Filter = "lev" };

            Assert.AreEqual(DisplayStyle.Flex, Field<IntegerField>(view, "Level").style.display.value);
            Assert.AreEqual(DisplayStyle.None, Field<TextField>(view, "Title").style.display.value);
        }

        [Test]
        public void ListFoldoutFollowsTheListLength()
        {
            var target = new Target();
            var view = new InspectorView(target);
            var foldout = view.Query<Foldout>().Where(f => f.text.StartsWith("Numbers")).First();

            foldout.value = true;
            view.Refresh();
            Assert.AreEqual(2, foldout.Query<IntegerField>().ToList().Count);

            target.numbers.Add(3);
            view.Refresh();

            Assert.AreEqual("Numbers (3)", foldout.text);
            Assert.AreEqual(3, foldout.Query<IntegerField>().ToList().Count);
        }

        [Test]
        public void ObjectReferenceAsksToOpenIt()
        {
            var host = new GameObject("inspected");
            var holder = new ReferenceHolder { other = host };
            object selected = null;
            var view = new InspectorView(holder);
            view.ObjectSelected += o => selected = o;

            var button = view.Query<Button>(className: "ui-inspector__value").First();
            Assert.AreEqual("inspected (GameObject)", button.text);
            Click(button);

            Assert.AreSame(host, selected);
            Object.Destroy(host);
        }

        private sealed class ReferenceHolder
        {
            public GameObject other;
        }

        [UnityTest]
        public IEnumerator DebugMenuOpensClosesAndGoesBack()
        {
            DebugMenu.Register("Cheats/Gold", new Target());
            DebugMenu.Open();
            yield return null;

            Assert.IsTrue(DebugMenu.IsOpen);
            Assert.IsTrue(DebugMenu.Entries.Any(e => e.path == "Cheats/Gold"));

            DebugMenu.Open(new Target());
            yield return null;
            DebugMenu.Back();
            Assert.IsTrue(DebugMenu.IsOpen);
            DebugMenu.Back();
            Assert.IsFalse(DebugMenu.IsOpen);

            DebugMenu.Toggle();
            Assert.IsTrue(DebugMenu.IsOpen);
            DebugMenu.Close();
            Assert.IsFalse(DebugMenu.IsOpen);
        }

        [UnityTest]
        public IEnumerator DebugMenuShowsBuiltInPages()
        {
            DebugMenu.Open();
            yield return null;

            var root = Object.FindAnyObjectByType<UIDocument>(FindObjectsInactive.Include);
            var texts = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
                .SelectMany(d => d.rootVisualElement.Query<Button>(className: "dbg-item").ToList())
                .Select(b => b.text).ToList();

            Assert.IsNotNull(root);
            Assert.IsTrue(texts.Any(t => t.StartsWith("Screen")));
            Assert.IsTrue(texts.Any(t => t.StartsWith("Time")));
            Assert.IsTrue(texts.Any(t => t.StartsWith("Scene Hierarchy")));
        }
    }
}
