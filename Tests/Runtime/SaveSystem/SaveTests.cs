using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using Core.SaveSystem.Backends;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.SaveSystem.Tests
{
    /// <summary>The static facade with its runner, on a throwaway file - needs Play Mode or a player.</summary>
    public class SaveTests
    {
        [Serializable]
        private class Progress
        {
            public int Level;
        }

        private string directory;
        private FileSaveBackend backend;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Application.temporaryCachePath, "SaveSystemTests", Path.GetRandomFileName());
            backend = new FileSaveBackend(Path.Combine(directory, "save.json"));
            Save.ResetForTests(backend);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            Save.ResetForTests(null);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void ValuesSurviveAReload()
        {
            Save.Set("progress", new Progress { Level = 4 });
            Save.SetString("raw", "text");
            Assert.IsTrue(Save.Flush());

            Save.ResetForTests(backend);
            Assert.AreEqual(4, Save.GetOrDefault<Progress>("progress").Level);
            Assert.IsTrue(Save.TryGetString("raw", out var raw));
            Assert.AreEqual("text", raw);
        }

        [Test]
        public void GetOrDefaultReturnsFallbackForMissingKey()
        {
            var fallback = new Progress { Level = -1 };
            Assert.AreSame(fallback, Save.GetOrDefault("missing", fallback));
        }

        [Test]
        public void ConfigureAfterFirstUseIsIgnored()
        {
            Save.Load();
            LogAssert.Expect(LogType.Error, new Regex("Configure must be called"));
            Save.Configure(new PlayerPrefsSaveBackend("core.savesystem.tests.unused"));

            Save.Set("progress", new Progress { Level = 1 });
            Save.Flush();
            Assert.IsTrue(File.Exists(backend.FilePath));
        }

        [Test]
        public void EventsReachSubscribersAddedAfterLoad()
        {
            Save.Load();
            var flushed = 0;
            var cleared = 0;
            Save.Flushed += () => flushed++;
            Save.Cleared += () => cleared++;

            Save.Flush(true);
            Save.Clear();

            Assert.AreEqual(1, flushed);
            Assert.AreEqual(1, cleared);
        }

        [UnityTest]
        public IEnumerator RunnerIsCreatedOnceAndSurvivesSceneObjects()
        {
            Save.Load();
            yield return null;

            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<SaveRunner>(FindObjectsSortMode.None).Length);
            Save.Has("anything");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<SaveRunner>(FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator AutoSaveWritesAfterTheDelay()
        {
            Save.AutoSaveDelay = 0.2f;
            Save.AutoSaveMaxDelay = 1f;
            Save.Set("progress", new Progress { Level = 2 });
            Assert.IsFalse(File.Exists(backend.FilePath));

            var deadline = Time.realtimeSinceStartup + 3f;
            while (Save.IsDirty && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsFalse(Save.IsDirty);
            Assert.IsTrue(File.Exists(backend.FilePath));
        }

        [UnityTest]
        public IEnumerator PlayTimeCountsWhileTimeScaleIsZero()
        {
            Save.Load();
            Time.timeScale = 0f;
            var start = Save.PlayTime;

            yield return new WaitForSecondsRealtime(0.3f);

            Assert.Greater(Save.PlayTime - start, 0.2);
        }
    }
}
