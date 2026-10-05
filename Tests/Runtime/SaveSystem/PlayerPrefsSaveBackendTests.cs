using Core.SaveSystem.Backends;
using NUnit.Framework;

namespace Core.SaveSystem.Tests
{
    public class PlayerPrefsSaveBackendTests
    {
        private const string Key = "core.savesystem.tests";

        private PlayerPrefsSaveBackend backend;

        [SetUp]
        public void SetUp()
        {
            backend = new PlayerPrefsSaveBackend(Key);
            backend.Delete();
        }

        [TearDown]
        public void TearDown()
        {
            backend.Delete();
            UnityEngine.PlayerPrefs.DeleteKey(Key + ".corrupt");
        }

        [Test]
        public void MissingKeyReadsAsNull()
        {
            Assert.IsNull(backend.Read());
            Assert.IsNull(backend.ReadBackup());
        }

        [Test]
        public void WriteKeepsPreviousAsBackup()
        {
            backend.Write("first");
            backend.Write("second");

            Assert.AreEqual("second", backend.Read());
            Assert.AreEqual("first", backend.ReadBackup());
        }

        [Test]
        public void QuarantineMovesValueAside()
        {
            backend.Write("broken");
            backend.QuarantineCorrupt();

            Assert.IsNull(backend.Read());
            Assert.AreEqual("broken", UnityEngine.PlayerPrefs.GetString(Key + ".corrupt"));
        }
    }
}
