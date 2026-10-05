using System;
using Core.Utilities.Notifications;
using NUnit.Framework;

namespace Core.SaveSystem.Tests
{
    public class LoadSaveInitializerTests
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
            Save.ResetForTests(backend);
            Notices.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            Save.ResetForTests(null);
            Notices.ResetForTests();
        }

        private string ValidSave()
        {
            var store = new SaveStore(new MemorySaveBackend());
            store.Set("progress", new Progress { Level = 3 });
            store.TryExportSnapshot(out var snapshot);
            return snapshot.Text;
        }

        private static void Load() => new LoadSaveInitializer().Initialize();

        [Test]
        public void HealthySaveLoadsWithoutNotices()
        {
            backend.Primary = ValidSave();

            Load();

            Assert.AreEqual(3, Save.GetOrDefault<Progress>("progress").Level);
            Assert.IsEmpty(Notices.Pending);
        }

        [Test]
        public void RestoredBackupIsAcknowledged()
        {
            backend.Backup = ValidSave();
            backend.Primary = "garbage";

            ExpectLog.SaveError(SaveErrorKind.Corrupt);
            Load();

            Assert.AreEqual(3, Save.GetOrDefault<Progress>("progress").Level);
            Assert.AreEqual(1, Notices.Pending.Count);
            var notice = Notices.Pending[0];
            Assert.AreEqual(LoadSaveInitializer.RestoredNoticeId, notice.Id);
            Assert.AreEqual(NoticeResponse.Acknowledge, notice.Response);
        }

        [Test]
        public void LostSaveIsAcknowledged()
        {
            backend.Primary = "garbage";

            ExpectLog.SaveError(SaveErrorKind.Corrupt);
            Load();

            Assert.AreEqual(1, Notices.Pending.Count);
            Assert.AreEqual(LoadSaveInitializer.LostNoticeId, Notices.Pending[0].Id);
        }

        [Test]
        public void UnreadableSaveReportsReadOnly()
        {
            backend.FailRead = true;

            ExpectLog.SaveError(SaveErrorKind.ReadFailed);
            Load();

            Assert.IsTrue(Save.IsReadOnly);
            Assert.AreEqual(1, Notices.Pending.Count);
            var notice = Notices.Pending[0];
            Assert.AreEqual(LoadSaveInitializer.ReadOnlyNoticeId, notice.Id);
            Assert.AreEqual(NoticeSeverity.Error, notice.Severity);
            StringAssert.Contains(Save.ReadOnlyReason, notice.FormatFallback());
        }
    }
}
