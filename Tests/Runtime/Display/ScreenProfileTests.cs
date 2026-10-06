using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.Display.Tests
{
    public class ScreenProfileTests
    {
        private readonly List<ScreenState> changed = new();
        private readonly List<ScreenState> classChanged = new();

        [SetUp]
        public void SetUp()
        {
            ScreenProfile.ResetForTests();
            changed.Clear();
            classChanged.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ScreenProfile.ResetForTests();
        }

        private static ScreenMetrics Phone(bool portrait = true) => portrait
            ? new ScreenMetrics(1080, 2400, 2.625f, default, true)
            : new ScreenMetrics(2400, 1080, 2.625f, default, true);

        private static ScreenMetrics Desktop(int width = 1920, int height = 1080) =>
            new(width, height, 1f, default, false);

        private static ScreenState Evaluate(ScreenMetrics metrics, float userScale = 1f) =>
            ScreenProfile.Evaluate(metrics, SizeClassThresholds.Default, userScale);

        private void Listen()
        {
            ScreenProfile.Changed += changed.Add;
            ScreenProfile.ClassChanged += classChanged.Add;
        }

        [Test]
        public void PhoneIsCompactPortraitMobile()
        {
            var state = Evaluate(Phone());

            Assert.AreEqual(SizeClass.Compact, state.SizeClass);
            Assert.AreEqual(Orientation.Portrait, state.Orientation);
            Assert.IsTrue(state.IsMobile);
            Assert.AreEqual(1080f / 2.625f, state.WidthDp, 0.01f);
            Assert.AreEqual(state.WidthDp, state.ShortSideDp);
        }

        [Test]
        public void RotationKeepsSizeClassAndFlipsOrientation()
        {
            var portrait = Evaluate(Phone());
            var landscape = Evaluate(Phone(false));

            Assert.AreEqual(portrait.SizeClass, landscape.SizeClass);
            Assert.AreEqual(Orientation.Landscape, landscape.Orientation);
            Assert.IsFalse(portrait.SameClass(landscape));
        }

        [Test]
        public void TabletIsMediumAndDesktopIsExpanded()
        {
            Assert.AreEqual(SizeClass.Medium, Evaluate(new ScreenMetrics(2560, 1600, 2f, default, true)).SizeClass);
            Assert.AreEqual(SizeClass.Expanded, Evaluate(Desktop()).SizeClass);
            Assert.AreEqual(SizeClass.Compact, Evaluate(Desktop(800, 500)).SizeClass);
        }

        [Test]
        public void ThresholdsAreInclusiveLowerBounds()
        {
            Assert.AreEqual(SizeClass.Medium, Evaluate(Desktop(1000, 600)).SizeClass);
            Assert.AreEqual(SizeClass.Compact, Evaluate(Desktop(1000, 599)).SizeClass);
            Assert.AreEqual(SizeClass.Expanded, Evaluate(Desktop(1600, 900)).SizeClass);
        }

        [Test]
        public void UserScaleShrinksTheRoomInDp()
        {
            var state = Evaluate(Desktop(), 2f);

            Assert.AreEqual(2f, state.Scale);
            Assert.AreEqual(540f, state.ShortSideDp, 0.01f);
            Assert.AreEqual(SizeClass.Compact, state.SizeClass);
        }

        [Test]
        public void SafeInsetsAreMeasuredFromEachEdge()
        {
            var state = Evaluate(new ScreenMetrics(1000, 2000, 2f, new Rect(10f, 50f, 970f, 1850f), true));

            Assert.AreEqual(new ScreenInsets(10f, 20f, 100f, 50f), state.SafeInsets);
            Assert.AreEqual(new ScreenInsets(5f, 10f, 50f, 25f), state.SafeInsets.Scaled(1f / state.Scale));
        }

        [Test]
        public void EmptySafeAreaAndUnknownDensityFallBack()
        {
            var metrics = new ScreenMetrics(800, 600, 0f, default, false);
            var state = Evaluate(metrics);

            Assert.AreEqual(1f, metrics.Density);
            Assert.AreEqual(new Rect(0f, 0f, 800f, 600f), metrics.SafeArea);
            Assert.AreEqual(default(ScreenInsets), state.SafeInsets);
        }

        [Test]
        public void OverrideReplacesTheRealScreen()
        {
            ScreenProfile.Override(Phone());

            Assert.IsTrue(ScreenProfile.IsOverridden);
            Assert.AreEqual(Phone(), ScreenProfile.Current.Metrics);

            ScreenProfile.ClearOverride();

            Assert.IsFalse(ScreenProfile.IsOverridden);
            Assert.AreEqual(ScreenMetrics.Read(), ScreenProfile.Current.Metrics);
        }

        [Test]
        public void ClassChangeRaisesBothEventsAndResizeOnlyChanged()
        {
            ScreenProfile.Override(Desktop());
            Listen();

            ScreenProfile.Override(Desktop(1920, 1000));
            Assert.AreEqual(1, changed.Count);
            Assert.AreEqual(0, classChanged.Count);

            ScreenProfile.Override(Phone());
            Assert.AreEqual(2, changed.Count);
            Assert.AreEqual(1, classChanged.Count);
            Assert.AreEqual(SizeClass.Compact, classChanged[0].SizeClass);
            Assert.AreEqual(ScreenProfile.Current, changed[1]);
        }

        [Test]
        public void SameScreenRaisesNothing()
        {
            ScreenProfile.Override(Desktop());
            Listen();

            ScreenProfile.Refresh();
            ScreenProfile.Override(Desktop());

            Assert.IsEmpty(changed);
            Assert.IsEmpty(classChanged);
        }

        [Test]
        public void ClassChangedComesBeforeChanged()
        {
            ScreenProfile.Override(Desktop());
            var order = new List<string>();
            ScreenProfile.Changed += _ => order.Add("changed");
            ScreenProfile.ClassChanged += _ => order.Add("class");

            ScreenProfile.Override(Phone());

            CollectionAssert.AreEqual(new[] { "class", "changed" }, order);
        }

        [Test]
        public void UserScaleIsClampedAndRaisesChanged()
        {
            ScreenProfile.Override(Desktop());
            Listen();

            ScreenProfile.UserScale = 10f;

            Assert.AreEqual(ScreenProfile.MaxUserScale, ScreenProfile.UserScale);
            Assert.AreEqual(ScreenProfile.MaxUserScale, ScreenProfile.Current.Scale);
            Assert.AreEqual(1, changed.Count);
        }

        [Test]
        public void ThresholdsChangeReclassifies()
        {
            ScreenProfile.Override(Desktop());
            Listen();

            ScreenProfile.Thresholds = new SizeClassThresholds(1200f, 2000f);

            Assert.AreEqual(SizeClass.Compact, ScreenProfile.Current.SizeClass);
            Assert.AreEqual(1, classChanged.Count);
        }

        [Test]
        public void FailingListenerDoesNotStopOthers()
        {
            ScreenProfile.Override(Desktop());
            ScreenProfile.Changed += _ => throw new InvalidOperationException("listener failed");
            Listen();

            LogAssert.Expect(LogType.Exception, new Regex("listener failed"));
            ScreenProfile.Override(Phone());

            Assert.AreEqual(1, changed.Count);
        }

        [Test]
        public void ResetClearsListenersAndSettings()
        {
            Listen();
            ScreenProfile.UserScale = 2f;
            ScreenProfile.Override(Phone());

            ScreenProfile.ResetForTests();
            changed.Clear();
            ScreenProfile.Override(Desktop());
            ScreenProfile.Override(Phone());

            Assert.AreEqual(1f, ScreenProfile.UserScale);
            Assert.IsEmpty(changed);
        }

        [Test]
        public void RunnerExistsInPlayMode()
        {
            Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<ScreenProfileRunner>());
        }
    }
}
