using System;
using System.IO;
using System.Threading;
using Core.SaveSystem.Cloud;
using NUnit.Framework;
using UnityEngine;

namespace Core.SaveSystem.Tests
{
    public class CloudSyncTests
    {
        [Serializable]
        private class Progress
        {
            public int Level;
        }

        private MemorySaveBackend backend;
        private SaveStore store;
        private FakeCloudProvider cloud;
        private CloudSync sync;

        [SetUp]
        public void SetUp()
        {
            backend = new MemorySaveBackend();
            store = new SaveStore(backend, "this-device");
            store.Load();
            cloud = new FakeCloudProvider();
            sync = new CloudSync(store, cloud);
        }

        private CloudSyncResult Sync() => sync.SyncAsync().Completed();

        private int LocalLevel() => store.TryGet("progress", out Progress progress) ? progress.Level : -1;

        private int CloudLevel()
        {
            var other = new SaveStore(new MemorySaveBackend());
            other.ReplaceWith(cloud.CloudSnapshot);
            return other.TryGet("progress", out Progress progress) ? progress.Level : -1;
        }

        private void SetLocal(int level) => store.Set("progress", new Progress { Level = level });

        private void SetCloud(int level) =>
            cloud.SaveFromOtherDevice(other => other.Set("progress", new Progress { Level = level }));

        private void SyncedAt(int level)
        {
            SetLocal(level);
            Assert.AreEqual(CloudSyncResult.Uploaded, Sync());
        }

        [Test]
        public void EmptyCloudReceivesLocalSave()
        {
            SetLocal(1);

            Assert.AreEqual(CloudSyncResult.Uploaded, Sync());
            Assert.AreEqual(1, CloudLevel());
            Assert.AreEqual(store.SnapshotId, store.SyncedSnapshotId);
            Assert.IsFalse(store.HasUnsyncedChanges);
        }

        [Test]
        public void SameSnapshotIsUpToDate()
        {
            SyncedAt(1);

            Assert.AreEqual(CloudSyncResult.UpToDate, Sync());
            Assert.AreEqual(1, cloud.Uploads);
        }

        [Test]
        public void LocalNewerIsUploadedWithoutAsking()
        {
            SyncedAt(1);
            SetLocal(2);
            var conflicts = 0;
            sync.ConflictDetected += _ => conflicts++;

            Assert.AreEqual(CloudSyncResult.Uploaded, Sync());
            Assert.AreEqual(2, CloudLevel());
            Assert.AreEqual(0, conflicts);
        }

        [Test]
        public void CloudNewerAsksThePlayer()
        {
            SyncedAt(1);
            SetCloud(5);
            CloudConflict raised = null;
            sync.ConflictDetected += conflict => raised = conflict;

            Assert.AreEqual(CloudSyncResult.Conflict, Sync());
            Assert.IsNotNull(raised);
            Assert.AreSame(raised, sync.PendingConflict);
            Assert.IsFalse(raised.LocalChanged, "only the cloud moved on");
            Assert.AreEqual("Other device", raised.Cloud.DeviceName);
            Assert.AreEqual(1, LocalLevel(), "nothing changes before the player decides");
            Assert.AreEqual(5, CloudLevel());
        }

        [Test]
        public void BothChangedAsksThePlayer()
        {
            SyncedAt(1);
            SetCloud(5);
            SetLocal(2);

            Assert.AreEqual(CloudSyncResult.Conflict, Sync());
            Assert.IsTrue(sync.PendingConflict.LocalChanged);
        }

        [Test]
        public void UnrelatedSavesOnBothSidesAskThePlayer()
        {
            SetCloud(5);
            SetLocal(2);

            Assert.AreEqual(CloudSyncResult.Conflict, Sync());
            Assert.IsTrue(sync.PendingConflict.LocalChanged);
        }

        [Test]
        public void PendingConflictBlocksFurtherSyncs()
        {
            SyncedAt(1);
            SetCloud(5);
            Sync();
            var downloads = cloud.Downloads;

            Assert.AreEqual(CloudSyncResult.Conflict, Sync());
            Assert.AreEqual(downloads, cloud.Downloads, "no network while waiting for the player");
        }

        [Test]
        public void KeepCloudReplacesLocal()
        {
            SyncedAt(1);
            SetCloud(5);
            Sync();
            var replaced = 0;
            store.Replaced += () => replaced++;

            Assert.AreEqual(CloudSyncResult.Downloaded, sync.ResolveConflictAsync(CloudConflictChoice.KeepCloud).Completed());
            Assert.AreEqual(5, LocalLevel());
            Assert.AreEqual(1, replaced);
            Assert.IsNull(sync.PendingConflict);
            Assert.IsFalse(store.HasUnsyncedChanges);
            Assert.AreEqual(CloudSyncResult.UpToDate, Sync());
        }

        [Test]
        public void KeepLocalOverwritesCloud()
        {
            SyncedAt(1);
            SetCloud(5);
            SetLocal(2);
            Sync();

            Assert.AreEqual(CloudSyncResult.Uploaded, sync.ResolveConflictAsync(CloudConflictChoice.KeepLocal).Completed());
            Assert.AreEqual(2, CloudLevel());
            Assert.IsNull(sync.PendingConflict);
        }

        [Test]
        public void KeepLocalUploadsChangesMadeDuringThePrompt()
        {
            SyncedAt(1);
            SetCloud(5);
            Sync();
            SetLocal(3);

            sync.ResolveConflictAsync(CloudConflictChoice.KeepLocal).Completed();
            Assert.AreEqual(3, CloudLevel());
        }

        [Test]
        public void KeepLocalWhileOfflineRetriesWithoutAskingAgain()
        {
            SyncedAt(1);
            SetCloud(5);
            SetLocal(2);
            Sync();
            cloud.Online = false;

            Assert.AreEqual(CloudSyncResult.Unavailable, sync.ResolveConflictAsync(CloudConflictChoice.KeepLocal).Completed());
            cloud.Online = true;

            Assert.AreEqual(CloudSyncResult.Uploaded, Sync());
            Assert.AreEqual(2, CloudLevel());
        }

        [Test]
        public void CloudChangingAgainAfterKeepLocalAsksAgain()
        {
            SyncedAt(1);
            SetCloud(5);
            Sync();
            cloud.Online = false;
            sync.ResolveConflictAsync(CloudConflictChoice.KeepLocal).Completed();
            cloud.Online = true;
            SetCloud(9);

            Assert.AreEqual(CloudSyncResult.Conflict, Sync());
        }

        [Test]
        public void BlankInstallTakesCloudWithoutAsking()
        {
            SetCloud(5);

            Assert.AreEqual(CloudSyncResult.Downloaded, Sync());
            Assert.AreEqual(5, LocalLevel());
            Assert.IsNull(sync.PendingConflict);
        }

        [Test]
        public void ResetLocalSaveIsUploadedNotRestored()
        {
            SyncedAt(1);
            store.Clear();

            Assert.AreEqual(CloudSyncResult.Uploaded, Sync());
            Assert.AreEqual(-1, CloudLevel());
            Assert.AreEqual(-1, LocalLevel());
        }

        [Test]
        public void RefusedSignInLeavesEverythingAlone()
        {
            cloud.AllowSignIn = false;
            SetLocal(1);

            Assert.AreEqual(CloudSyncResult.Unavailable, Sync());
            Assert.IsNull(cloud.CloudText);
            Assert.IsNotNull(sync.LastError);
        }

        [Test]
        public void FailedUploadIsRetriedOnNextSync()
        {
            SetLocal(1);
            cloud.FailUploads = true;

            Assert.AreEqual(CloudSyncResult.Failed, Sync());
            Assert.IsTrue(store.HasUnsyncedChanges);

            cloud.FailUploads = false;
            Assert.AreEqual(CloudSyncResult.Uploaded, Sync());
            Assert.IsFalse(store.HasUnsyncedChanges);
        }

        /// <summary>Signed in when the sync starts, gone by the time the download runs.</summary>
        private sealed class DroppingProvider : ICloudSaveProvider
        {
            public bool Dropped;
            public string Name => "Dropping cloud";
            public bool IsAvailable => !Dropped;

            public Awaitable<bool> SignInAsync(CancellationToken cancellationToken) => Done(!Dropped);

            public Awaitable<string> DownloadAsync(CancellationToken cancellationToken)
            {
                Dropped = true;
                throw new IOException("connection reset");
            }

            public Awaitable UploadAsync(SaveSnapshot snapshot, CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            private static Awaitable<T> Done<T>(T value)
            {
                var source = new AwaitableCompletionSource<T>();
                source.SetResult(value);
                return source.Awaitable;
            }
        }

        [Test]
        public void ConnectionLostMidSyncIsUnavailableNotFailed()
        {
            SetLocal(1);
            var dropping = new CloudSync(store, new DroppingProvider());

            Assert.AreEqual(CloudSyncResult.Unavailable, dropping.SyncAsync().Completed());
            StringAssert.Contains("connection reset", dropping.LastError);
        }

        [Test]
        public void UnreadableCloudSaveIsNotOverwritten()
        {
            cloud.CloudText = "garbage";
            SetLocal(1);

            Assert.AreEqual(CloudSyncResult.Failed, Sync());
            Assert.AreEqual("garbage", cloud.CloudText);
        }

        [Test]
        public void NewerFormatCloudSaveIsNotOverwritten()
        {
            SetCloud(5);
            var newer = cloud.CloudText.Replace($"\"FormatVersion\":{SaveStore.FormatVersion}", "\"FormatVersion\":99");
            cloud.CloudText = newer;
            SetLocal(1);

            Assert.AreEqual(CloudSyncResult.Failed, Sync());
            Assert.AreEqual(newer, cloud.CloudText);
        }

        [Test]
        public void ReadOnlyLocalSaveIsNotSynced()
        {
            backend.FailRead = true;
            ExpectLog.SaveError(SaveErrorKind.ReadFailed);
            var readOnly = new SaveStore(backend);
            readOnly.Load();
            sync = new CloudSync(readOnly, cloud);

            Assert.AreEqual(CloudSyncResult.Skipped, Sync());
            Assert.AreEqual(0, cloud.Downloads);
        }

        [Test]
        public void SyncFinishedReportsEveryOutcome()
        {
            var results = new System.Collections.Generic.List<CloudSyncResult>();
            sync.SyncFinished += results.Add;
            SetLocal(1);
            Sync();
            SetCloud(5);
            Sync();

            CollectionAssert.AreEqual(new[] { CloudSyncResult.Uploaded, CloudSyncResult.Conflict }, results);
        }
    }
}
