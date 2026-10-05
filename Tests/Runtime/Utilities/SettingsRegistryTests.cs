using System;
using System.Text.RegularExpressions;
using Core.Utilities.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.Utilities.Tests
{
    public class SettingsRegistryTests
    {
        private Func<Type, bool, SettingsAsset> editorResolver;
        private TestPreloadedSettings preloaded;
        private TestAsyncSettings asynchronous;

        [SetUp]
        public void SetUp()
        {
            editorResolver = SettingsRegistry.EditorResolver;
            SettingsRegistry.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            SettingsRegistry.EditorResolver = editorResolver;
            SettingsRegistry.ResetForTests();
            if (preloaded) UnityEngine.Object.DestroyImmediate(preloaded);
            if (asynchronous) UnityEngine.Object.DestroyImmediate(asynchronous);
        }

        // What the editor would find on disk: the in-memory instances made by the test.
        private void UseEditorResolver()
        {
            SettingsRegistry.EditorResolver = (type, create) =>
            {
                if (type == typeof(TestPreloadedSettings)) return preloaded;
                if (type == typeof(TestAsyncSettings)) return asynchronous || create ? Async() : null;
                return null;
            };
        }

        private TestAsyncSettings Async() =>
            asynchronous ? asynchronous : asynchronous = ScriptableObject.CreateInstance<TestAsyncSettings>();

        [Test]
        public void BuildFindsPreloadedSettingsInMemory()
        {
            SettingsRegistry.EditorResolver = null;
            preloaded = ScriptableObject.CreateInstance<TestPreloadedSettings>();

            Assert.AreSame(preloaded, TestPreloadedSettings.Instance);
            Assert.IsTrue(TestPreloadedSettings.TryGet(out var found));
            Assert.AreSame(preloaded, found);
        }

        [Test]
        public void BuildThrowsForMissingPreloadedSettings()
        {
            SettingsRegistry.EditorResolver = null;

            Assert.IsFalse(TestPreloadedSettings.TryGet(out var found));
            Assert.IsNull(found);
            var exception = Assert.Throws<InvalidOperationException>(() => _ = TestPreloadedSettings.Instance);
            StringAssert.Contains("Preloaded Assets", exception.Message);
        }

        [Test]
        public void BuildThrowsForAsyncSettingsBeforeTheyLoad()
        {
            SettingsRegistry.EditorResolver = null;
            // Even in memory, an async type is only what LoadAllAsync registered.
            Async();

            Assert.IsFalse(TestAsyncSettings.TryGet(out _));
            var exception = Assert.Throws<InvalidOperationException>(() => _ = TestAsyncSettings.Instance);
            StringAssert.Contains("LoadAllAsync", exception.Message);
        }

        [Test]
        public void EditorResolvesEveryTypeSynchronously()
        {
            preloaded = ScriptableObject.CreateInstance<TestPreloadedSettings>();
            UseEditorResolver();

            Assert.AreSame(preloaded, TestPreloadedSettings.Instance);
            Assert.AreSame(Async(), TestAsyncSettings.Instance);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void EditorTryGetNeverCreates()
        {
            UseEditorResolver();

            Assert.IsFalse(TestAsyncSettings.TryGet(out _));
            Assert.IsFalse(asynchronous);
            Assert.IsNotNull(TestAsyncSettings.Instance);
            Assert.IsTrue(TestAsyncSettings.TryGet(out var found));
            Assert.AreSame(asynchronous, found);
        }

        [Test]
        public void EditorRefindsDestroyedAsset()
        {
            UseEditorResolver();
            var first = TestAsyncSettings.Instance;
            UnityEngine.Object.DestroyImmediate(first);
            asynchronous = null;

            var second = TestAsyncSettings.Instance;

            Assert.IsTrue(second);
            Assert.AreNotSame(first, second);
        }

        [Test]
        public void EditorReportsAsyncReadDuringBootInitializersOnce()
        {
            preloaded = ScriptableObject.CreateInstance<TestPreloadedSettings>();
            UseEditorResolver();
            SettingsRegistry.PreloadedOnly = true;

            LogAssert.Expect(LogType.Error, new Regex(@"\[Settings\] TestAsyncSettings was read during boot initializers"));
            Assert.IsNotNull(TestAsyncSettings.Instance);
            Assert.IsNotNull(TestPreloadedSettings.Instance);
            UnityEngine.Object.DestroyImmediate(asynchronous);
            asynchronous = null;
            Assert.IsNotNull(TestAsyncSettings.Instance);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void EditorStaysQuietWhenNothingLoadsSettings()
        {
            UseEditorResolver();

            Assert.IsNotNull(TestAsyncSettings.Instance);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
