using System;
using System.IO;
using Core.SaveSystem.Backends;
using NUnit.Framework;
using UnityEngine;

namespace Core.SaveSystem.Tests
{
    // Runs on the real file system of the platform, which is the point of running it in a player.
    public class FileSaveBackendTests
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
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void MissingFileReadsAsNull()
        {
            Assert.IsNull(backend.Read());
            Assert.IsNull(backend.ReadBackup());
        }

        [Test]
        public void WriteKeepsPreviousAsBackup()
        {
            backend.Write("first");
            Assert.AreEqual("first", backend.Read());
            Assert.IsNull(backend.ReadBackup());

            backend.Write("second");
            Assert.AreEqual("second", backend.Read());
            Assert.AreEqual("first", backend.ReadBackup());
            Assert.IsFalse(File.Exists(backend.FilePath + ".tmp"));
        }

        [Test]
        public void WritesNonAsciiText()
        {
            backend.Write("zażółć gęślą jaźń");
            Assert.AreEqual("zażółć gęślą jaźń", backend.Read());
        }

        [Test]
        public void QuarantineMovesFileAside()
        {
            backend.Write("broken");
            backend.QuarantineCorrupt();

            Assert.IsNull(backend.Read());
            Assert.AreEqual(1, Directory.GetFiles(directory, "save.json.corrupt-*").Length);
        }

        [Test]
        public void DeleteRemovesSaveAndBackup()
        {
            backend.Write("first");
            backend.Write("second");
            backend.Delete();

            Assert.IsNull(backend.Read());
            Assert.IsNull(backend.ReadBackup());
        }

        [Test]
        public void StoreRecoversFromTruncatedFileOnDisk()
        {
            var store = new SaveStore(backend);
            store.Set("progress", new Progress { Level = 1 });
            store.Flush();
            store.Set("progress", new Progress { Level = 2 });
            store.Flush();

            var text = File.ReadAllText(backend.FilePath);
            File.WriteAllText(backend.FilePath, text.Substring(0, text.Length / 2));

            var reloaded = new SaveStore(backend);
            ExpectLog.SaveError(SaveErrorKind.Corrupt);
            Assert.IsTrue(reloaded.TryGet("progress", out Progress progress));
            Assert.AreEqual(1, progress.Level, "the backup holds the write before the damaged one");
            Assert.AreEqual(1, Directory.GetFiles(directory, "save.json.corrupt-*").Length);

            Assert.IsTrue(reloaded.Flush());
            Assert.IsTrue(new SaveStore(backend).TryGet("progress", out Progress _));
        }
    }
}
