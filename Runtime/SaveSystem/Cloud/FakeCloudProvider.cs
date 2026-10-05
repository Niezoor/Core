using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Core.SaveSystem.Cloud
{
    /// <summary>
    /// A cloud that lives in memory (or in a local file, to survive Play Mode sessions), for tests and for building
    /// the sync UI before a real service exists. Every knob a real service can turn against you is a property:
    /// latency, being offline, refusing sign-in, failing uploads, another device saving in between.
    /// </summary>
    public sealed class FakeCloudProvider : ICloudSaveProvider
    {
        public string Name => "Fake cloud";
        public bool IsAvailable => signedIn && Online;

        /// <summary>False makes every call fail as if there were no network.</summary>
        public bool Online { get; set; } = true;
        public bool AllowSignIn { get; set; } = true;
        public bool FailUploads { get; set; }
        /// <summary>Seconds each call takes, in unscaled time.</summary>
        public float Latency { get; set; }

        public int Downloads { get; private set; }
        public int Uploads { get; private set; }

        /// <summary>What the cloud holds; null when empty.</summary>
        public string CloudText
        {
            get => persistPath == null ? cloudText : File.Exists(persistPath) ? File.ReadAllText(persistPath) : null;
            set
            {
                if (persistPath == null)
                {
                    cloudText = value;
                    return;
                }

                if (value == null)
                {
                    if (File.Exists(persistPath)) File.Delete(persistPath);
                    return;
                }

                var directory = Path.GetDirectoryName(persistPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(persistPath, value);
            }
        }

        public SaveSnapshot CloudSnapshot => SaveSnapshot.TryParse(CloudText, out var snapshot) ? snapshot : null;

        private readonly string persistPath;
        private string cloudText;
        private bool signedIn;

        /// <param name="persistPath">Optional file standing in for the cloud, so it outlives a Play Mode session.</param>
        public FakeCloudProvider(string persistPath = null)
        {
            this.persistPath = persistPath;
        }

        public async Awaitable<bool> SignInAsync(CancellationToken cancellationToken)
        {
            await DelayAsync(cancellationToken);
            signedIn = AllowSignIn && Online;
            return signedIn;
        }

        public async Awaitable<string> DownloadAsync(CancellationToken cancellationToken)
        {
            await DelayAsync(cancellationToken);
            ThrowIfOffline();
            Downloads++;
            return CloudText;
        }

        public async Awaitable UploadAsync(SaveSnapshot snapshot, CancellationToken cancellationToken)
        {
            await DelayAsync(cancellationToken);
            ThrowIfOffline();
            if (FailUploads) throw new IOException("Upload rejected by the fake cloud");
            Uploads++;
            CloudText = snapshot.Text;
        }

        /// <summary>Another device loads what the cloud holds, applies <paramref name="edit"/> and uploads it.</summary>
        public void SaveFromOtherDevice(Action<SaveStore> edit, string deviceName = "Other device")
        {
            var other = new SaveStore(new ScratchBackend(), deviceName);
            other.Load();
            if (SaveSnapshot.TryParse(CloudText, out var current)) other.ReplaceWith(current);
            edit(other);
            other.TryExportSnapshot(out var snapshot);
            CloudText = snapshot.Text;
        }

        private void ThrowIfOffline()
        {
            if (!Online)
            {
                signedIn = false;
                throw new IOException("The fake cloud is offline");
            }
        }

        private async Awaitable DelayAsync(CancellationToken cancellationToken)
        {
            var end = Time.realtimeSinceStartup + Latency;
            while (Time.realtimeSinceStartup < end) await Awaitable.NextFrameAsync(cancellationToken);
        }

        private sealed class ScratchBackend : ISaveBackend
        {
            private string primary;
            private string backup;

            public string Read() => primary;
            public string ReadBackup() => backup;

            public void Write(string text)
            {
                backup = primary;
                primary = text;
            }

            public void QuarantineCorrupt() => primary = null;

            public void Delete()
            {
                primary = null;
                backup = null;
            }
        }
    }
}
