using System;
using System.Collections.Generic;
using System.IO;
using Core.SaveSystem.Backends;
using UnityEngine;

namespace Core.SaveSystem
{
    /// <summary>
    /// Game-facing access to the one <see cref="SaveStore"/>. Loads synchronously on first use, autosaves a short
    /// while after changes, and writes on pause, focus loss and quit. Once the application is quitting every change
    /// is written immediately, so state saved from OnDestroy or OnApplicationQuit is never lost to ordering.
    /// </summary>
    public static class Save
    {
        private static ISaveBackend configuredBackend;
        private static SaveStore store;
        private static SaveRunner runner;
        private static bool quitting;

        public static event Action BeforeFlush
        {
            add => Store.BeforeFlush += value;
            remove { if (store != null) store.BeforeFlush -= value; }
        }

        public static event Action Flushed
        {
            add => Store.Flushed += value;
            remove { if (store != null) store.Flushed -= value; }
        }

        public static event Action Cleared
        {
            add => Store.Cleared += value;
            remove { if (store != null) store.Cleared -= value; }
        }

        /// <inheritdoc cref="SaveStore.Replaced"/>
        public static event Action Replaced
        {
            add => Store.Replaced += value;
            remove { if (store != null) store.Replaced -= value; }
        }

        public static event Action<SaveError> ErrorRaised
        {
            add => Store.ErrorRaised += value;
            remove { if (store != null) store.ErrorRaised -= value; }
        }

        public static bool IsDirty => Store.IsDirty;
        public static bool IsReadOnly => Store.IsReadOnly;
        public static string ReadOnlyReason => Store.ReadOnlyReason;
        public static SaveError LastError => Store.LastError;
        public static double PlayTime => Store.PlayTime;
        public static DateTime LastModifiedUtc => Store.LastModifiedUtc;
        public static IReadOnlyCollection<string> Keys => Store.Keys;
        public static string SnapshotId => Store.SnapshotId;
        public static long Revision => Store.Revision;
        public static string SyncedSnapshotId => Store.SyncedSnapshotId;
        public static bool HasUnsyncedChanges => Store.HasUnsyncedChanges;

        public static string Description
        {
            get => Store.Description;
            set
            {
                Store.Description = value;
                FlushIfQuitting();
            }
        }

        public static bool AutoSave
        {
            get => Store.AutoSave;
            set => Store.AutoSave = value;
        }

        public static float AutoSaveDelay
        {
            get => Store.AutoSaveDelay;
            set => Store.AutoSaveDelay = value;
        }

        public static float AutoSaveMaxDelay
        {
            get => Store.AutoSaveMaxDelay;
            set => Store.AutoSaveMaxDelay = value;
        }

        internal static SaveStore Store
        {
            get
            {
                if (store == null)
                {
                    store = new SaveStore(configuredBackend ?? CreateDefaultBackend(), SystemInfo.deviceName);
                    store.Load();
                    Application.quitting -= OnQuitting;
                    Application.quitting += OnQuitting;
                }

                if (!runner && Application.isPlaying && !quitting) runner = SaveRunner.Create();
                return store;
            }
        }

        /// <summary>
        /// Replaces the default backend. Only before the first access - call it from a
        /// <c>RuntimeInitializeOnLoadMethod(BeforeSceneLoad)</c>.
        /// </summary>
        public static void Configure(ISaveBackend backend)
        {
            if (store != null)
            {
                Debug.LogError("[Save] Configure must be called before the save is first used; ignored.");
                return;
            }

            configuredBackend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        /// <summary>
        /// A file in persistentDataPath (PlayerPrefs on WebGL). The editor uses its own file, so play sessions
        /// never touch a build's save on the same machine.
        /// </summary>
        public static ISaveBackend CreateDefaultBackend()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new PlayerPrefsSaveBackend("core.savesystem.save");
#else
            var fileName = Application.isEditor ? "save_editor.json" : "save.json";
            return new FileSaveBackend(Path.Combine(Application.persistentDataPath, fileName));
#endif
        }

        /// <summary>Loads now rather than on first use, e.g. from a boot scene.</summary>
        public static void Load() => _ = Store;

        public static bool Has(string key) => Store.Has(key);

        public static bool TryGet<T>(string key, out T value) => Store.TryGet(key, out value);

        public static T GetOrDefault<T>(string key, T fallback = default) =>
            Store.TryGet(key, out T value) ? value : fallback;

        public static bool TryGetString(string key, out string value) => Store.TryGetString(key, out value);

        public static void Set<T>(string key, T value)
        {
            Store.Set(key, value);
            FlushIfQuitting();
        }

        public static void SetString(string key, string value)
        {
            Store.SetString(key, value);
            FlushIfQuitting();
        }

        public static bool Remove(string key)
        {
            var removed = Store.Remove(key);
            FlushIfQuitting();
            return removed;
        }

        /// <inheritdoc cref="SaveStore.Flush"/>
        public static bool Flush(bool force = false) => Store.Flush(force);

        /// <inheritdoc cref="SaveStore.Clear"/>
        public static void Clear() => Store.Clear();

        /// <inheritdoc cref="SaveStore.TryExportSnapshot"/>
        public static bool TryExportSnapshot(out SaveSnapshot snapshot) => Store.TryExportSnapshot(out snapshot);

        /// <inheritdoc cref="SaveStore.ReplaceWith"/>
        public static bool ReplaceWith(SaveSnapshot snapshot) => Store.ReplaceWith(snapshot);

        /// <inheritdoc cref="SaveStore.MarkSynced"/>
        public static void MarkSynced(string snapshotId)
        {
            Store.MarkSynced(snapshotId);
            FlushIfQuitting();
        }

        internal static void Tick(float deltaTime) => store?.Tick(deltaTime);

        private static void FlushIfQuitting()
        {
            if (quitting) store.Flush();
        }

        private static void OnQuitting()
        {
            quitting = true;
            store?.Flush(true);
        }

        /// <summary>Drops the store and runner and starts over on <paramref name="backend"/> (the default when null).</summary>
        internal static void ResetForTests(ISaveBackend backend)
        {
            if (runner)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(runner.gameObject);
                else UnityEngine.Object.DestroyImmediate(runner.gameObject);
            }

            ResetStatics();
            configuredBackend = backend;
        }

        // Domain reload is off in Enter Play Mode settings, so nothing static may survive into the next session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Application.quitting -= OnQuitting;
            configuredBackend = null;
            store = null;
            runner = null;
            quitting = false;
        }
    }
}
