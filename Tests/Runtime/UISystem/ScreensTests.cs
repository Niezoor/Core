using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Display;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Core.UISystem.Tests
{
    public class ScreensTests
    {
        private static readonly ScreenMetrics Desktop = new(1920, 1080, 1f, default, false);
        private static readonly ScreenMetrics Phone = new(1080, 2400, 2.625f, default, true);

        [SetUp]
        public void SetUp()
        {
            Reset();
            ScreenProfile.Override(Desktop);
        }

        [TearDown]
        public void TearDown() => Reset();

        private static void Reset()
        {
            ScreenProfile.ResetForTests();
            UIInput.ResetForTests();
            DebugMenu.ResetForTests();
            Screens.ResetForTests();
            TestLog.Entries.Clear();
        }

        private static IEnumerator Frames(int count = 3)
        {
            for (var i = 0; i < count; i++) yield return null;
        }

        /// <summary>Lets the default 0.15 s transitions finish.</summary>
        private static IEnumerator Settle()
        {
            var end = Time.unscaledTime + 0.5f;
            while (Time.unscaledTime < end) yield return null;
        }

        [Test]
        public void PushBuildsShowsAndFocuses()
        {
            var a = Screens.Push<ScreenA>();

            CollectionAssert.AreEqual(new[] { a }, Screens.Stack);
            Assert.AreSame(a, Screens.Top);
            Assert.IsTrue(a.IsOpen && a.IsVisible && a.IsFocused);
            CollectionAssert.AreEqual(new[] { "ScreenA.Create", "ScreenA.Show", "ScreenA.Focus" }, TestLog.Entries);
            Assert.IsTrue(Screens.RootElement.Contains(a.Root));
        }

        [UnityTest]
        public IEnumerator ScreenOverScreenHidesTheOneBelow()
        {
            var a = Screens.Push<ScreenA>();
            TestLog.Entries.Clear();
            var b = Screens.Push<ScreenB>();

            CollectionAssert.AreEqual(new[] { "ScreenA.Blur", "ScreenA.Hide", "ScreenB.Create", "ScreenB.Show", "ScreenB.Focus" }.OrderBy(e => e),
                TestLog.Entries.OrderBy(e => e));
            Assert.IsTrue(TestLog.Entries.IndexOf("ScreenA.Blur") < TestLog.Entries.IndexOf("ScreenB.Focus"));
            Assert.IsFalse(a.IsVisible);
            Assert.IsTrue(b.IsVisible && b.IsFocused);

            yield return Settle();

            Assert.AreEqual(DisplayStyle.None, a.Root.style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, b.Root.style.display.value);
        }

        [Test]
        public void PopupKeepsTheScreenBelowVisible()
        {
            var a = Screens.Push<ScreenA>();
            var p = Screens.Push<PopupP>();

            Assert.IsTrue(a.IsVisible);
            Assert.IsFalse(a.IsFocused);
            Assert.IsTrue(p.IsFocused);
            Assert.IsTrue(a.Root.ClassListContains("ui-screen--covered"));
            Assert.AreEqual(PickingMode.Position, p.Root.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, a.Root.pickingMode);
        }

        [UnityTest]
        public IEnumerator ClosingTheTopRefocusesTheOneBelowAndDestroysIt()
        {
            var a = Screens.Push<ScreenA>();
            var b = Screens.Push<ScreenB>();
            TestLog.Entries.Clear();

            b.Close();

            Assert.IsFalse(b.IsOpen);
            Assert.IsTrue(a.IsVisible && a.IsFocused);
            CollectionAssert.Contains(TestLog.Entries, "ScreenA.Show");
            CollectionAssert.Contains(TestLog.Entries, "ScreenA.Focus");
            CollectionAssert.DoesNotContain(TestLog.Entries, "ScreenB.Destroy");

            yield return Settle();

            CollectionAssert.Contains(TestLog.Entries, "ScreenB.Destroy");
            Assert.IsNull(b.Root);
        }

        [Test]
        public void ClosingAScreenInTheMiddleKeepsTheOthers()
        {
            var a = Screens.Push<ScreenA>();
            var b = Screens.Push<ScreenB>();
            var p = Screens.Push<PopupP>();

            b.Close();

            CollectionAssert.AreEqual(new UIScreen[] { a, p }, Screens.Stack);
            Assert.IsTrue(a.IsVisible);
            Assert.IsTrue(p.IsFocused);
        }

        [Test]
        public void BackClosesTheTopButNotTheLastScreen()
        {
            var unhandled = 0;
            Screens.BackUnhandled += () => unhandled++;
            var a = Screens.Push<ScreenA>();
            var b = Screens.Push<ScreenB>();

            Assert.IsTrue(Screens.Back());
            Assert.IsFalse(b.IsOpen);
            Assert.IsFalse(Screens.Back());

            Assert.IsTrue(a.IsOpen);
            Assert.AreEqual(1, unhandled);
        }

        [Test]
        public void ScreenCanRefuseBack()
        {
            Screens.Push<ScreenA>();
            var b = Screens.Push<ScreenB>();
            b.UseDefaultBack = false;
            b.BackResult = true;

            Assert.IsTrue(Screens.Back());
            Assert.IsTrue(b.IsOpen);
            CollectionAssert.Contains(TestLog.Entries, "ScreenB.Back");
        }

        [UnityTest]
        public IEnumerator ResultReachesTheCaller()
        {
            Screens.Push<ScreenA>();
            var popup = Screens.Push<ConfirmPopup>();
            bool? first = null, second = null;

            async Awaitable Wait()
            {
                first = await popup.WaitForResultAsync();
            }

            async Awaitable WaitToo()
            {
                second = await popup.WaitForResultAsync();
            }

            _ = Wait();
            _ = WaitToo();
            popup.Close(true);
            yield return Frames(2);

            Assert.AreEqual(true, first);
            Assert.AreEqual(true, second);
        }

        [UnityTest]
        public IEnumerator ClosedWithoutResultGivesDefault()
        {
            Screens.Push<ScreenA>();
            var popup = Screens.Push<ConfirmPopup>();
            bool? result = null;

            async Awaitable Wait()
            {
                result = await popup.WaitForResultAsync();
            }

            _ = Wait();
            Screens.Back();
            yield return Frames(2);

            Assert.AreEqual(false, result);
        }

        [Test]
        public void KeepAliveScreenIsReused()
        {
            var first = Screens.Push<KeptK>();
            first.Close();
            var second = Screens.Push<KeptK>();

            Assert.AreSame(first, second);
            Assert.AreEqual(1, TestLog.Entries.Count(e => e == "KeptK.Create"));
            Assert.IsTrue(second.IsOpen && second.IsVisible);
        }

        [Test]
        public void ReplaceSwapsTheTop()
        {
            var a = Screens.Push<ScreenA>();
            var b = Screens.Replace<ScreenB>();

            CollectionAssert.AreEqual(new UIScreen[] { b }, Screens.Stack);
            Assert.IsFalse(a.IsOpen);
        }

        [Test]
        public void ClearClosesEverythingAtOnce()
        {
            var a = Screens.Push<ScreenA>();
            var o = Screens.Push<OverlayO>();

            Screens.Clear();

            Assert.IsEmpty(Screens.Stack);
            Assert.IsEmpty(Screens.Overlays);
            Assert.IsFalse(a.IsOpen || o.IsOpen);
            CollectionAssert.Contains(TestLog.Entries, "ScreenA.Destroy");
            CollectionAssert.Contains(TestLog.Entries, "OverlayO.Destroy");
        }

        [Test]
        public void OverlayStaysOutsideTheStack()
        {
            var a = Screens.Push<ScreenA>();
            var o = Screens.Push<OverlayO>();

            CollectionAssert.AreEqual(new UIScreen[] { a }, Screens.Stack);
            CollectionAssert.AreEqual(new UIScreen[] { o }, Screens.Overlays);
            Assert.AreSame(a, Screens.Top);
            Assert.IsFalse(o.IsFocused);

            Screens.Back();
            Assert.IsTrue(o.IsOpen);
        }

        [Test]
        public void GetFindsTheTopmostOfAType()
        {
            Screens.Push<ScreenA>();
            var second = Screens.Push<ScreenA>();

            Assert.AreSame(second, Screens.Get<ScreenA>());
            Assert.IsTrue(Screens.IsOpen<ScreenA>());
            Assert.IsFalse(Screens.IsOpen<ScreenB>());
        }

        [Test]
        public void RootCarriesProfileAndInputClasses()
        {
            var root = Screens.RootElement;
            Assert.IsTrue(root.ClassListContains("ui-size-expanded"));
            Assert.IsTrue(root.ClassListContains("ui-desktop"));

            ScreenProfile.Override(Phone);
            UIInput.SetMode(InputMode.Gamepad);

            Assert.IsTrue(root.ClassListContains("ui-size-compact"));
            Assert.IsFalse(root.ClassListContains("ui-size-expanded"));
            Assert.IsTrue(root.ClassListContains("ui-portrait"));
            Assert.IsTrue(root.ClassListContains("ui-mobile"));
            Assert.IsTrue(root.ClassListContains("ui-input-gamepad"));
            Assert.IsTrue(root.ClassListContains("ui-input-navigation"));
        }

        [Test]
        public void LayoutVariantIsSwappedWhenTheProfileChanges()
        {
            var compact = ScriptableObject.CreateInstance<VisualTreeAsset>();
            var wide = ScriptableObject.CreateInstance<VisualTreeAsset>();
            Screens.ViewResolver = (_, state) => state.SizeClass == SizeClass.Compact ? compact : wide;

            var screen = Screens.Push<UxmlScreen>();
            Assert.AreSame(wide, screen.viewAsset);

            ScreenProfile.Override(Phone);

            Assert.AreSame(compact, screen.viewAsset);
            CollectionAssert.AreEqual(new[] { "UxmlScreen.Create", "UxmlScreen.Destroy", "UxmlScreen.Create" }, TestLog.Entries);
            Assert.IsTrue(screen.IsVisible && screen.IsFocused);

            UnityEngine.Object.Destroy(compact);
            UnityEngine.Object.Destroy(wide);
        }

        [Test]
        public void MissingViewThrowsAClearError()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => Screens.Push<UxmlScreen>());

            StringAssert.Contains("UxmlScreen has no view", exception.Message);
            Assert.IsEmpty(Screens.Stack);
        }

        [Test]
        public void FailingCallbackDoesNotBreakTheStack()
        {
            LogAssert.Expect(LogType.Exception, new Regex("show failed"));

            var screen = Screens.Push<FailingScreen>();

            Assert.AreSame(screen, Screens.Top);
            Assert.IsTrue(screen.IsFocused);
        }

        [UnityTest]
        public IEnumerator NavigationModeFocusesTheTopScreen()
        {
            UIInput.SetMode(InputMode.Keyboard);
            var a = Screens.Push<ScreenA>();
            yield return Frames();

            var focused = Screens.Panel.focusController.focusedElement as VisualElement;
            Assert.AreSame(a.Root.Q<Button>("first"), focused);
        }

        [UnityTest]
        public IEnumerator FocusReturnsWhereItWasAfterAPopup()
        {
            UIInput.SetMode(InputMode.Keyboard);
            var a = Screens.Push<ScreenA>();
            yield return Frames();
            a.Root.Q<Button>("second").Focus();
            yield return null;

            var popup = Screens.Push<PopupP>();
            yield return Frames();
            Assert.AreSame(popup.Root.Q<Button>("first"), Screens.Panel.focusController.focusedElement);

            popup.Close();
            yield return Frames();
            Assert.AreSame(a.Root.Q<Button>("second"), Screens.Panel.focusController.focusedElement);
        }

        [UnityTest]
        public IEnumerator FocusCannotStayInACoveredScreen()
        {
            UIInput.SetMode(InputMode.Keyboard);
            var a = Screens.Push<ScreenA>();
            var popup = Screens.Push<PopupP>();
            yield return Frames();

            a.Root.Q<Button>("second").Focus();
            yield return Frames();

            var focused = Screens.Panel.focusController.focusedElement as VisualElement;
            Assert.IsTrue(popup.Root.Contains(focused));
        }

        [UnityTest]
        public IEnumerator PointerModeDoesNotMoveFocus()
        {
            UIInput.SetMode(InputMode.Pointer);
            Screens.Push<ScreenA>();
            yield return Frames();

            Assert.IsNull(Screens.Panel.focusController.focusedElement);
        }

        [UnityTest]
        public IEnumerator SafeAreaPadsByTheInsets()
        {
            ScreenProfile.Override(new ScreenMetrics(1000, 2000, 1f, new Rect(0f, 100f, 1000f, 1800f), true));
            var screen = Screens.Push<ScreenA>();
            var safeArea = new SafeArea { Edges = SafeAreaEdges.Top | SafeAreaEdges.Bottom };
            screen.Root.Add(safeArea);
            yield return Frames();

            Assert.AreEqual(100f, safeArea.style.paddingTop.value.value, 0.5f);
            Assert.AreEqual(100f, safeArea.style.paddingBottom.value.value, 0.5f);
            Assert.AreEqual(0f, safeArea.style.paddingLeft.value.value, 0.01f);
        }

        [Test]
        public void PanelScalesWithTheProfile()
        {
            Screens.Push<ScreenA>();
            ScreenProfile.UserScale = 2f;

            var settings = Screens.RootElement.panel is { } panel ? Panels.PanelUnitsPerPixel(panel) : 0f;
            Assert.Greater(settings, 0f);
            Assert.AreEqual(2f, ScreenProfile.Current.Scale);
        }

        [UnityTest]
        public IEnumerator IsPointerOverUIOnlyWhereSomethingPicks()
        {
            Screens.Push<ScreenA>();
            Screens.Push<PopupP>();
            yield return Frames();

            // A popup's scrim covers the whole screen.
            Assert.IsTrue(Screens.IsPointerOverUI(new Vector2(5f, 5f)));
            Screens.Back();
            Screens.Clear();
            Assert.IsFalse(Screens.IsPointerOverUI(new Vector2(5f, 5f)));
        }
    }
}
