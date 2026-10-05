using System;
using NUnit.Framework;

namespace Core.SaveSystem.Tests
{
    public class SaveSnapshotTests
    {
        [Serializable]
        private class Progress
        {
            public int Level;
        }

        private MemorySaveBackend backend;

        [SetUp]
        public void SetUp()
        {
            backend = new MemorySaveBackend();
        }

        private SaveStore NewStore(string device = "local")
        {
            var store = new SaveStore(backend, device);
            store.Load();
            return store;
        }

        private static SaveSnapshot RemoteSnapshot(int level, string description = "remote")
        {
            var store = new SaveStore(new MemorySaveBackend(), "remote-device");
            store.Set("progress", new Progress { Level = level });
            store.Description = description;
            store.TryExportSnapshot(out var snapshot);
            return snapshot;
        }

        [Test]
        public void ContentWriteCreatesNewSnapshot()
        {
            var store = NewStore();
            Assert.IsNull(store.SnapshotId);

            store.Set("progress", new Progress { Level = 1 });
            store.Flush();
            var first = store.SnapshotId;
            Assert.IsNotNull(first);
            Assert.AreEqual(1, store.Revision);

            store.Set("progress", new Progress { Level = 2 });
            store.Flush();
            Assert.AreNotEqual(first, store.SnapshotId);
            Assert.AreEqual(2, store.Revision);
        }

        [Test]
        public void MetadataOnlyWriteKeepsSnapshot()
        {
            var store = NewStore();
            store.Set("progress", new Progress { Level = 1 });
            store.Flush();
            var id = store.SnapshotId;
            var modified = store.LastModifiedUtc;

            store.PlayTime += 100;
            store.Flush(true);

            Assert.AreEqual(id, store.SnapshotId);
            Assert.AreEqual(1, store.Revision);
            Assert.AreEqual(modified, store.LastModifiedUtc);
            Assert.AreEqual(2, backend.Writes, "play time is still written");
        }

        [Test]
        public void ExportWritesPendingChangesFirst()
        {
            var store = NewStore();
            store.Set("progress", new Progress { Level = 3 });

            Assert.IsTrue(store.TryExportSnapshot(out var snapshot));
            Assert.IsFalse(store.IsDirty);
            Assert.AreEqual(store.SnapshotId, snapshot.SnapshotId);
            Assert.AreEqual("local", snapshot.DeviceName);
            Assert.AreEqual(1, backend.Writes);
        }

        [Test]
        public void ExportOfFreshSaveStillHasAnId()
        {
            var store = NewStore();
            Assert.IsTrue(store.TryExportSnapshot(out var snapshot));
            Assert.IsNotNull(snapshot.SnapshotId);
        }

        [Test]
        public void ExportLeavesOutSyncBookkeeping()
        {
            var store = NewStore();
            store.Set("progress", new Progress { Level = 1 });
            store.TryExportSnapshot(out var first);
            store.MarkSynced(first.SnapshotId);
            store.Flush();

            StringAssert.Contains($"\"SyncedSnapshotId\":\"{first.SnapshotId}\"", backend.Primary);
            store.TryExportSnapshot(out var second);
            StringAssert.Contains("\"SyncedSnapshotId\":\"\"", second.Text);
        }

        [Test]
        public void SnapshotTextRoundTrips()
        {
            var original = RemoteSnapshot(5, "slot A");

            Assert.IsTrue(SaveSnapshot.TryParse(original.Text, out var parsed));
            Assert.AreEqual(original.SnapshotId, parsed.SnapshotId);
            Assert.AreEqual(original.Revision, parsed.Revision);
            Assert.AreEqual("slot A", parsed.Description);
            Assert.AreEqual("remote-device", parsed.DeviceName);
            Assert.AreEqual(original.ModifiedUtc, parsed.ModifiedUtc);
        }

        [Test]
        public void ParseRejectsTextWithoutSnapshotId()
        {
            Assert.IsFalse(SaveSnapshot.TryParse("{\"FormatVersion\":1,\"Entries\":[]}", out _));
            Assert.IsFalse(SaveSnapshot.TryParse("not json", out _));
            Assert.IsFalse(SaveSnapshot.TryParse(null, out _));
        }

        [Test]
        public void ReplaceSwapsContentAndCountsAsSynced()
        {
            var store = NewStore();
            store.Set("progress", new Progress { Level = 1 });
            store.Set("local-only", new Progress { Level = 9 });
            store.Flush();
            store.Set("progress", new Progress { Level = 2 });

            var replaced = 0;
            store.Replaced += () => replaced++;
            var remote = RemoteSnapshot(7);

            Assert.IsTrue(store.ReplaceWith(remote));
            Assert.AreEqual(1, replaced);
            Assert.IsTrue(store.TryGet("progress", out Progress progress));
            Assert.AreEqual(7, progress.Level);
            Assert.IsFalse(store.Has("local-only"));
            Assert.AreEqual("remote", store.Description);
            Assert.AreEqual(remote.SnapshotId, store.SnapshotId);
            Assert.AreEqual(remote.SnapshotId, store.SyncedSnapshotId);
            Assert.IsFalse(store.HasUnsyncedChanges);

            var reloaded = NewStore();
            Assert.AreEqual(remote.SnapshotId, reloaded.SnapshotId);
            Assert.AreEqual(remote.SnapshotId, reloaded.SyncedSnapshotId);
        }

        [Test]
        public void ReplaceDoesNotLetBeforeFlushWriteOldStateBack()
        {
            var store = NewStore();
            var inMemoryLevel = 1;
            store.BeforeFlush += () => store.Set("progress", new Progress { Level = inMemoryLevel });

            store.ReplaceWith(RemoteSnapshot(7));

            Assert.IsFalse(store.IsDirty);
            Assert.IsTrue(NewStore().TryGet("progress", out Progress progress));
            Assert.AreEqual(7, progress.Level);
        }

        [Test]
        public void ReplaceWithNewerFormatIsRefused()
        {
            var store = NewStore();
            store.Set("progress", new Progress { Level = 1 });
            store.Flush();

            var newer = RemoteSnapshot(7).Text.Replace($"\"FormatVersion\":{SaveStore.FormatVersion}", "\"FormatVersion\":99");
            Assert.IsTrue(SaveSnapshot.TryParse(newer, out var snapshot));

            ExpectLog.SaveError(SaveErrorKind.NewerFormat);
            Assert.IsFalse(store.ReplaceWith(snapshot));
            Assert.AreEqual(SaveErrorKind.NewerFormat, store.LastError.Kind);
            Assert.IsTrue(store.TryGet("progress", out Progress progress));
            Assert.AreEqual(1, progress.Level);
        }

        [Test]
        public void ReplaceIsRefusedWhileReadOnly()
        {
            backend.FailRead = true;
            ExpectLog.SaveError(SaveErrorKind.ReadFailed);
            var store = NewStore();

            var remote = RemoteSnapshot(7);
            ExpectLog.SaveError(SaveErrorKind.ReplaceRefused);
            Assert.IsFalse(store.ReplaceWith(remote));
            Assert.AreEqual(SaveErrorKind.ReplaceRefused, store.LastError.Kind);
        }

        [Test]
        public void MarkSyncedIsPersistedWithoutNewSnapshot()
        {
            var store = NewStore();
            store.Set("progress", new Progress { Level = 1 });
            store.TryExportSnapshot(out var uploaded);

            store.MarkSynced(uploaded.SnapshotId);
            Assert.IsFalse(store.HasUnsyncedChanges);
            Assert.IsTrue(store.Flush());
            Assert.AreEqual(uploaded.SnapshotId, store.SnapshotId);

            var reloaded = NewStore();
            Assert.AreEqual(uploaded.SnapshotId, reloaded.SyncedSnapshotId);
            Assert.IsFalse(reloaded.HasUnsyncedChanges);
        }

        [Test]
        public void ChangeAfterSyncIsUnsynced()
        {
            var store = NewStore();
            store.Set("progress", new Progress { Level = 1 });
            store.TryExportSnapshot(out var uploaded);
            store.MarkSynced(uploaded.SnapshotId);

            store.Set("progress", new Progress { Level = 2 });
            Assert.IsTrue(store.HasUnsyncedChanges);
            store.Flush();
            Assert.IsTrue(store.HasUnsyncedChanges);
            Assert.AreEqual(uploaded.SnapshotId, store.SyncedSnapshotId);
        }

        [Test]
        public void AutoSaveWritesSyncMarker()
        {
            var store = NewStore();
            store.AutoSaveDelay = 1f;
            store.Set("progress", new Progress { Level = 1 });
            store.TryExportSnapshot(out var uploaded);
            var writes = backend.Writes;

            store.MarkSynced(uploaded.SnapshotId);
            store.Tick(1.1f);

            Assert.AreEqual(writes + 1, backend.Writes);
            Assert.AreEqual(uploaded.SnapshotId, NewStore().SyncedSnapshotId);
        }

        [Test]
        public void FormatV1SaveLoadsAndUpgradesOnWrite()
        {
            backend.Primary = "{\"FormatVersion\":1,\"PlayTimeTicks\":0,\"ModifiedUtcTicks\":0,\"Description\":\"old\"," +
                              "\"Entries\":[{\"Key\":\"progress\",\"Version\":0,\"Json\":\"{\\\"Level\\\":4}\"}]}";

            var store = NewStore();
            Assert.IsFalse(store.IsReadOnly);
            Assert.IsNull(store.SnapshotId);
            Assert.IsFalse(store.HasUnsyncedChanges);
            Assert.IsTrue(store.TryGet("progress", out Progress progress));
            Assert.AreEqual(4, progress.Level);

            store.Flush(true);
            StringAssert.Contains($"\"FormatVersion\":{SaveStore.FormatVersion}", backend.Primary);
            Assert.IsNotNull(store.SnapshotId);
        }

        [Test]
        public void ClearForgetsSnapshotButKeepsSyncBase()
        {
            var store = NewStore();
            store.Set("progress", new Progress { Level = 1 });
            store.TryExportSnapshot(out var uploaded);
            store.MarkSynced(uploaded.SnapshotId);

            store.Clear();
            Assert.IsNull(store.SnapshotId);
            Assert.AreEqual(0, store.Revision);
            Assert.AreEqual(uploaded.SnapshotId, store.SyncedSnapshotId);

            Assert.IsTrue(store.Flush());
            Assert.AreEqual(uploaded.SnapshotId, NewStore().SyncedSnapshotId, "the sync base survives a restart");
        }
    }
}
