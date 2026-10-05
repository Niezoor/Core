using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Core.SaveSystem.Backends;
using Core.SaveSystem.Cloud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.SaveSystem.Tests
{
    /// <summary>The static facade with its runner and real frame-by-frame latency - needs Play Mode or a player.</summary>
    public class CloudSaveTests
    {
        [Serializable]
        private class Progress
        {
            public int Level;
        }

        private string directory;
        private FakeCloudProvider cloud;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Application.temporaryCachePath, "CloudSaveTests", Path.GetRandomFileName());
            Save.ResetForTests(new FileSaveBackend(Path.Combine(directory, "save.json")));
            CloudSave.ResetForTests();
            cloud = new FakeCloudProvider { Latency = 0.1f };
        }

        [TearDown]
        public void TearDown()
        {
            CloudSave.ResetForTests();
            Save.ResetForTests(null);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeout = 5f)
        {
            var deadline = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("timed out");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator SyncsOnItsOwnAfterRegistering()
        {
            Save.Set("progress", new Progress { Level = 1 });
            CloudSave.Register(cloud);

            yield return WaitUntil(() => cloud.Uploads == 1);
            Assert.AreEqual(CloudSyncResult.Uploaded, CloudSave.LastResult);
            Assert.IsFalse(Save.HasUnsyncedChanges);
        }

        [UnityTest]
        public IEnumerator IsSyncingWhileTheServiceIsSlow()
        {
            CloudSave.AutoSync = false;
            CloudSave.Register(cloud);
            Save.Set("progress", new Progress { Level = 1 });

            var sync = CloudSave.SyncAsync();
            Assert.IsTrue(CloudSave.IsSyncing);

            CloudSyncResult result = default;
            yield return sync.Wait(r => result = r);
            Assert.AreEqual(CloudSyncResult.Uploaded, result);
            Assert.IsFalse(CloudSave.IsSyncing);
        }

        [UnityTest]
        public IEnumerator ConflictReachesTheGameAndCanBeResolved()
        {
            Save.Set("progress", new Progress { Level = 1 });
            CloudSave.Register(cloud);
            yield return WaitUntil(() => cloud.Uploads == 1);

            cloud.SaveFromOtherDevice(other => other.Set("progress", new Progress { Level = 5 }));
            CloudConflict raised = null;
            CloudSave.ConflictDetected += conflict => raised = conflict;

            var sync = CloudSave.SyncAsync();
            yield return sync.Wait(_ => { });
            Assert.IsNotNull(raised);
            Assert.AreSame(raised, CloudSave.PendingConflict);

            var reloaded = 0;
            Save.Replaced += () => reloaded++;
            yield return CloudSave.ResolveConflictAsync(CloudConflictChoice.KeepCloud).Wait(_ => { });

            Assert.AreEqual(5, Save.GetOrDefault<Progress>("progress").Level);
            Assert.AreEqual(1, reloaded);
            Assert.IsNull(CloudSave.PendingConflict);
        }

        [UnityTest]
        public IEnumerator OfflineRetriesAfterTheDelay()
        {
            CloudSave.RetryDelay = 0.3f;
            cloud.Online = false;
            Save.Set("progress", new Progress { Level = 1 });
            CloudSave.Register(cloud);

            yield return WaitUntil(() => CloudSave.LastResult == CloudSyncResult.Unavailable);
            cloud.Online = true;

            yield return WaitUntil(() => cloud.Uploads == 1);
        }

        [Test]
        public void StatusIsDisabledWithoutAProvider()
        {
            Assert.AreEqual(CloudStatus.Disabled, CloudSave.Status);
        }

        [UnityTest]
        public IEnumerator StatusGoesFromLoadingToSynced()
        {
            var changes = new List<CloudStatus>();
            CloudSave.StatusChanged += changes.Add;
            CloudStatus seenBySyncFinished = default;
            CloudSave.SyncFinished += _ => seenBySyncFinished = CloudSave.Status;

            Save.Set("progress", new Progress { Level = 1 });
            CloudSave.Register(cloud);
            Assert.AreEqual(CloudStatus.Loading, CloudSave.Status);

            yield return WaitUntil(() => cloud.Uploads == 1);
            Assert.AreEqual(CloudStatus.Synced, CloudSave.Status);
            Assert.AreEqual(CloudStatus.Synced, seenBySyncFinished);
            CollectionAssert.AreEqual(new[] { CloudStatus.Loading, CloudStatus.Synced }, changes);
        }

        [UnityTest]
        public IEnumerator RoutineSyncsDoNotChangeTheStatus()
        {
            CloudSave.AutoSync = false;
            CloudSave.Register(cloud);
            Save.Set("progress", new Progress { Level = 1 });
            yield return CloudSave.SyncAsync().Wait(_ => { });

            var changes = new List<CloudStatus>();
            CloudSave.StatusChanged += changes.Add;
            Save.Set("progress", new Progress { Level = 2 });
            var sync = CloudSave.SyncAsync();
            Assert.IsTrue(CloudSave.IsSyncing);
            Assert.AreEqual(CloudStatus.Synced, CloudSave.Status);
            yield return sync.Wait(_ => { });

            Assert.IsEmpty(changes);
        }

        [UnityTest]
        public IEnumerator StatusIsOfflineUntilTheServiceComesBack()
        {
            CloudSave.RetryDelay = 0.3f;
            cloud.Online = false;
            CloudSave.Register(cloud);

            yield return WaitUntil(() => CloudSave.Status == CloudStatus.Offline);
            cloud.Online = true;

            yield return WaitUntil(() => CloudSave.Status == CloudStatus.Synced);
        }

        [UnityTest]
        public IEnumerator StatusIsErrorWhenTheCloudSaveIsUnreadable()
        {
            CloudSave.AutoSync = false;
            cloud.CloudText = "not a save";
            CloudSave.Register(cloud);

            CloudSyncResult result = default;
            yield return CloudSave.SyncAsync().Wait(r => result = r);

            Assert.AreEqual(CloudSyncResult.Failed, result);
            Assert.AreEqual(CloudStatus.Error, CloudSave.Status);
        }

        [UnityTest]
        public IEnumerator StatusShowsAConflictUntilItIsResolved()
        {
            Save.Set("progress", new Progress { Level = 1 });
            CloudSave.Register(cloud);
            yield return WaitUntil(() => cloud.Uploads == 1);

            cloud.SaveFromOtherDevice(other => other.Set("progress", new Progress { Level = 5 }));
            CloudStatus seenByConflictDetected = default;
            CloudSave.ConflictDetected += _ => seenByConflictDetected = CloudSave.Status;
            yield return CloudSave.SyncAsync().Wait(_ => { });

            Assert.AreEqual(CloudStatus.Conflict, CloudSave.Status);
            Assert.AreEqual(CloudStatus.Conflict, seenByConflictDetected);

            yield return CloudSave.ResolveConflictAsync(CloudConflictChoice.KeepLocal).Wait(_ => { });
            Assert.AreEqual(CloudStatus.Synced, CloudSave.Status);
        }

        [Test]
        public void HigherPriorityProviderWins()
        {
            var platform = new FakeCloudProvider();
            CloudSave.Register(platform, 0);
            CloudSave.Register(cloud, 10);
            CloudSave.Register(new FakeCloudProvider(), 5);

            Assert.AreSame(cloud, CloudSave.Provider);
        }

        [Test]
        public void ProviderCannotChangeOnceSyncStarted()
        {
            cloud.Latency = 0f;
            CloudSave.AutoSync = false;
            CloudSave.Register(cloud);
            CloudSave.SyncAsync().Completed();

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("registered after cloud sync started"));
            CloudSave.Register(new FakeCloudProvider(), 100);
            Assert.AreSame(cloud, CloudSave.Provider);
        }
    }
}
