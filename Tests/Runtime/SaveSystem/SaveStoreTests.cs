using System;
using NUnit.Framework;

namespace Core.SaveSystem.Tests
{
    public class SaveStoreTests
    {
        [Serializable]
        private class Progress
        {
            public int Level;
        }

        [Serializable, SaveVersion(2)]
        private class ProgressV2 : ISaveMigration
        {
            public int Stage;
            public int MigratedFrom = -1;

            [Serializable]
            private class V1
            {
                public int Level;
            }

            public void Migrate(int fromVersion, string json)
            {
                MigratedFrom = fromVersion;
                Stage = UnityEngine.JsonUtility.FromJson<V1>(json).Level;
            }
        }

        private MemorySaveBackend backend;

        [SetUp]
        public void SetUp()
        {
            backend = new MemorySaveBackend();
        }

        private SaveStore NewStore()
        {
            var store = new SaveStore(backend);
            store.Load();
            return store;
        }

        private string SavedWith(Action<SaveStore> fill)
        {
            var capture = new MemorySaveBackend();
            var writer = new SaveStore(capture);
            fill(writer);
            writer.Flush();
            return capture.Primary;
        }

        [Test]
        public void RoundTripsThroughBackend()
        {
            var store = NewStore();
            store.Set("progress", new Progress { Level = 7 });
            store.SetString("raw", "hello");
            store.Description = "slot";
            store.PlayTime = 12.5;
            Assert.IsTrue(store.Flush());

            var reloaded = NewStore();
            Assert.IsTrue(reloaded.TryGet("progress", out Progress progress));
            Assert.AreEqual(7, progress.Level);
            Assert.IsTrue(reloaded.TryGetString("raw", out var raw));
            Assert.AreEqual("hello", raw);
            Assert.AreEqual("slot", reloaded.Description);
            Assert.AreEqual(12.5, reloaded.PlayTime, 1e-6);
            Assert.AreNotEqual(default(DateTime), reloaded.LastModifiedUtc);
        }

        [Test]
        public void MissingEntryIsNotFound()
        {
            var store = NewStore();
            Assert.IsFalse(store.Has("progress"));
            Assert.IsFalse(store.TryGet("progress", out Progress _));
        }

        [Test]
        public void ChangesStayInMemoryUntilFlushed()
        {
            var store = NewStore();
            store.Set("progress", new Progress { Level = 1 });
            Assert.IsTrue(store.IsDirty);
            Assert.AreEqual(0, backend.Writes);

            store.Flush();
            Assert.IsFalse(store.IsDirty);
            Assert.AreEqual(1, backend.Writes);

            store.Set("progress", new Progress { Level = 1 });
            Assert.IsFalse(store.IsDirty, "an identical value is not a change");
        }

        [Test]
        public void CorruptSaveIsQuarantinedAndBackupRestored()
        {
            var store = NewStore();
            store.Set("progress", new Progress { Level = 3 });
            store.Flush();
            backend.Backup = backend.Primary;
            backend.Primary = "{\"FormatVersion\":1,\"Entr";

            ExpectLog.SaveError(SaveErrorKind.Corrupt);
            var reloaded = NewStore();
            Assert.AreEqual("{\"FormatVersion\":1,\"Entr", backend.Quarantined);
            Assert.IsTrue(reloaded.TryGet("progress", out Progress progress));
            Assert.AreEqual(3, progress.Level);
            Assert.IsTrue(reloaded.IsDirty, "the restored save should be written back as the primary");
            Assert.IsFalse(reloaded.IsReadOnly);
        }

        [Test]
        public void CorruptSaveWithoutBackupStartsEmptyButKeepsTheFile()
        {
            backend.Primary = "\0\0\0\0";

            ExpectLog.SaveError(SaveErrorKind.Corrupt);
            var store = NewStore();
            Assert.AreEqual("\0\0\0\0", backend.Quarantined);
            Assert.IsFalse(store.IsReadOnly);
            Assert.AreEqual(SaveErrorKind.Corrupt, store.LastError.Kind);
            Assert.IsEmpty(store.Keys);
        }

        [Test]
        public void MissingPrimaryFallsBackToBackup()
        {
            backend.Backup = SavedWith(s => s.Set("progress", new Progress { Level = 5 }));

            var store = NewStore();
            Assert.IsTrue(store.TryGet("progress", out Progress progress));
            Assert.AreEqual(5, progress.Level);
        }

        [Test]
        public void UnreadableBackendMakesStoreReadOnly()
        {
            backend.Primary = SavedWith(s => s.Set("progress", new Progress { Level = 1 }));
            var original = backend.Primary;
            backend.FailRead = true;

            ExpectLog.SaveError(SaveErrorKind.ReadFailed);
            var store = NewStore();
            Assert.IsTrue(store.IsReadOnly);
            store.Set("progress", new Progress { Level = 2 });
            Assert.IsFalse(store.Flush(true));
            Assert.AreEqual(original, backend.Primary);
        }

        [Test]
        public void NewerFormatMakesStoreReadOnly()
        {
            backend.Primary = "{\"FormatVersion\":99,\"Entries\":[]}";

            ExpectLog.SaveError(SaveErrorKind.NewerFormat);
            var store = NewStore();
            Assert.IsTrue(store.IsReadOnly);
            Assert.AreEqual(SaveErrorKind.NewerFormat, store.LastError.Kind);
            Assert.IsFalse(store.Flush(true));
            Assert.AreEqual(0, backend.Writes);
        }

        [Test]
        public void EntryFromNewerVersionIsLockedButPreserved()
        {
            var store = NewStore();
            store.Set("progress", new ProgressV2 { Stage = 4 });
            store.Set("other", new Progress { Level = 1 });
            store.Flush();

            var older = NewStore();
            ExpectLog.SaveError(SaveErrorKind.EntryNewerVersion);
            Assert.IsFalse(older.TryGet("progress", out Progress _));
            ExpectLog.SaveError(SaveErrorKind.EntryLocked);
            older.Set("progress", new Progress { Level = 9 });
            older.Set("other", new Progress { Level = 2 });
            older.Flush();

            var reloaded = NewStore();
            Assert.IsTrue(reloaded.TryGet("progress", out ProgressV2 progress));
            Assert.AreEqual(4, progress.Stage);
            Assert.IsTrue(reloaded.TryGet("other", out Progress other));
            Assert.AreEqual(2, other.Level, "other entries stay writable");
        }

        [Test]
        public void EntryFromOlderVersionIsMigratedFromRawJson()
        {
            var store = NewStore();
            store.Set("progress", new Progress { Level = 6 });
            store.Flush();

            var reloaded = NewStore();
            Assert.IsTrue(reloaded.TryGet("progress", out ProgressV2 progress));
            Assert.AreEqual(0, progress.MigratedFrom);
            Assert.AreEqual(6, progress.Stage);
        }

        [Test]
        public void FailedWriteKeepsChangesDirty()
        {
            var store = NewStore();
            store.Set("progress", new Progress { Level = 1 });
            backend.FailWrite = true;

            ExpectLog.SaveError(SaveErrorKind.WriteFailed);
            Assert.IsFalse(store.Flush());
            Assert.IsTrue(store.IsDirty);
            Assert.AreEqual(SaveErrorKind.WriteFailed, store.LastError.Kind);

            backend.FailWrite = false;
            Assert.IsTrue(store.Flush());
            Assert.IsFalse(store.IsDirty);
        }

        [Test]
        public void AutoSaveWaitsForQuietThenWrites()
        {
            var store = NewStore();
            store.AutoSaveDelay = 1f;
            store.AutoSaveMaxDelay = 5f;
            store.Set("progress", new Progress { Level = 1 });

            store.Tick(0.6f);
            store.Set("progress", new Progress { Level = 2 });
            store.Tick(0.6f);
            Assert.AreEqual(0, backend.Writes, "a new change restarts the quiet period");

            store.Tick(0.5f);
            Assert.AreEqual(1, backend.Writes);
            Assert.AreEqual(1.7, store.PlayTime, 1e-4);
        }

        [Test]
        public void AutoSaveWritesAtMaxDelayEvenWhileChanging()
        {
            var store = NewStore();
            store.AutoSaveDelay = 1f;
            store.AutoSaveMaxDelay = 2f;

            for (var i = 0; i < 5; i++)
            {
                store.Set("progress", new Progress { Level = i });
                store.Tick(0.5f);
            }

            Assert.AreEqual(1, backend.Writes);
        }

        [Test]
        public void BeforeFlushChangesAreIncluded()
        {
            var store = NewStore();
            store.BeforeFlush += () => store.Set("late", new Progress { Level = 8 });
            store.Flush(true);

            Assert.IsTrue(NewStore().TryGet("late", out Progress late));
            Assert.AreEqual(8, late.Level);
        }

        [Test]
        public void ThrowingSubscriberDoesNotStopTheWrite()
        {
            var store = NewStore();
            store.BeforeFlush += () => throw new InvalidOperationException();
            store.Set("progress", new Progress { Level = 1 });

            ExpectLog.Exception<InvalidOperationException>();
            Assert.IsTrue(store.Flush());
            Assert.AreEqual(1, backend.Writes);
        }

        [Test]
        public void ClearDeletesEverything()
        {
            var store = NewStore();
            store.Set("progress", new Progress { Level = 1 });
            store.PlayTime = 10;
            store.Flush();

            store.Clear();
            Assert.IsNull(backend.Primary);
            Assert.IsNull(backend.Backup);
            Assert.IsEmpty(store.Keys);
            Assert.AreEqual(0, store.PlayTime);
            Assert.IsFalse(store.IsDirty);
        }

        [Test]
        public void TypesJsonUtilityCannotRoundTripAreRejected()
        {
            var store = NewStore();
            Assert.Throws<ArgumentException>(() => store.Set("n", 5));
            Assert.Throws<ArgumentException>(() => store.Set("s", "text"));
            Assert.Throws<ArgumentException>(() => store.Set("l", new System.Collections.Generic.List<int>()));
        }
    }
}
