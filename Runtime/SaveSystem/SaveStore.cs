using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.SaveSystem
{
    /// <summary>
    /// The save as keyed, versioned JSON entries held in memory and written through an <see cref="ISaveBackend"/>.
    /// A plain object so it can be tested without a scene; <see cref="Save"/> is the game-facing facade.
    ///
    /// The one rule it never breaks: a save it could not read cleanly is never overwritten. An unreadable file is
    /// moved aside before anything is written, and a file it cannot get at at all makes the store read-only.
    /// </summary>
    public sealed class SaveStore
    {
        public const int FormatVersion = 2;

        /// <summary>Raised right before a write - the moment to put in-memory state into the save.</summary>
        public event Action BeforeFlush;
        public event Action Flushed;
        public event Action Cleared;
        /// <summary>
        /// The whole save was swapped for a snapshot (from the cloud). Anything holding save state in memory must
        /// read it again - writing its old copy back would undo the swap.
        /// </summary>
        public event Action Replaced;
        public event Action<SaveError> ErrorRaised;

        public bool AutoSave { get; set; } = true;
        /// <summary>Seconds without a change before a dirty save is written.</summary>
        public float AutoSaveDelay { get; set; } = 2f;
        /// <summary>Upper bound on how long a save that keeps changing stays unwritten.</summary>
        public float AutoSaveMaxDelay { get; set; } = 10f;

        public bool IsLoaded { get; private set; }
        public bool IsDirty { get; private set; }
        public bool IsReadOnly => ReadOnlyReason != null;
        public string ReadOnlyReason { get; private set; }
        public SaveError LastError { get; private set; }
        public double PlayTime { get; set; }
        public DateTime LastModifiedUtc { get; private set; }
        public IReadOnlyCollection<string> Keys => entries.Keys;

        /// <summary>Identity of the content on disk; null until the first write. See <see cref="SaveSnapshot"/>.</summary>
        public string SnapshotId { get; private set; }
        /// <summary>Count of content writes, carried over from a snapshot that replaced the save.</summary>
        public long Revision { get; private set; }
        /// <summary>The snapshot the cloud was last known to hold.</summary>
        public string SyncedSnapshotId { get; private set; }
        public bool HasUnsyncedChanges => IsDirty || SnapshotId != SyncedSnapshotId;

        public string Description
        {
            get => description;
            set
            {
                if (description == value) return;
                description = value;
                MarkDirty();
            }
        }

        private readonly ISaveBackend backend;
        private readonly string deviceName;
        private readonly Dictionary<string, SaveEntry> entries = new();
        private readonly HashSet<string> lockedKeys = new();
        private string description;
        // Something to write that is not a content change: play time, sync bookkeeping.
        private bool metadataDirty;
        private bool flushing;
        private float dirtyAge;
        private float idleTime;

        /// <param name="deviceName">Stamped on every snapshot, for a conflict prompt to show where a save came from.</param>
        public SaveStore(ISaveBackend backend, string deviceName = null)
        {
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            this.deviceName = deviceName;
        }

        public void Load()
        {
            if (IsLoaded) return;
            IsLoaded = true;

            string text;
            try
            {
                text = backend.Read();
            }
            catch (Exception exception)
            {
                MakeReadOnly(SaveErrorKind.ReadFailed, $"Could not read the save at {backend}", exception);
                return;
            }

            if (text != null)
            {
                if (SaveFileData.TryParse(text, out var data))
                {
                    Apply(data);
                    return;
                }

                try
                {
                    backend.QuarantineCorrupt();
                }
                catch (Exception exception)
                {
                    MakeReadOnly(SaveErrorKind.Corrupt, $"The save at {backend} is unreadable and could not be moved aside", exception);
                    return;
                }

                Raise(new SaveError(SaveErrorKind.Corrupt, $"The save at {backend} is unreadable; moved aside, trying the backup"));
            }

            string backup;
            try
            {
                backup = backend.ReadBackup();
            }
            catch (Exception exception)
            {
                // Writing now would rotate the unread backup away.
                MakeReadOnly(SaveErrorKind.ReadFailed, $"Could not read the backup at {backend}", exception);
                return;
            }

            if (backup == null) return;

            if (SaveFileData.TryParse(backup, out var restored))
            {
                Apply(restored);
                MarkDirty();
                Debug.LogWarning($"[Save] Restored from the backup at {backend}");
                return;
            }

            Raise(new SaveError(SaveErrorKind.Corrupt, $"The backup at {backend} is unreadable too; starting empty"));
        }

        public bool Has(string key)
        {
            EnsureLoaded();
            return key != null && entries.ContainsKey(key);
        }

        /// <summary>
        /// Reads an entry into a new value. False when it is missing, unreadable, or saved by a newer version of
        /// <typeparamref name="T"/> - in which case it is also locked against writes.
        /// </summary>
        public bool TryGet<T>(string key, out T value)
        {
            value = default;
            ThrowIfUnsupported(typeof(T));
            EnsureLoaded();
            if (key == null || !entries.TryGetValue(key, out var entry) || lockedKeys.Contains(key)) return false;

            var codeVersion = SaveVersionAttribute.Of(typeof(T));
            if (entry.Version > codeVersion)
            {
                lockedKeys.Add(key);
                Raise(new SaveError(SaveErrorKind.EntryNewerVersion,
                    $"'{key}' was saved as {typeof(T).Name} v{entry.Version}, this build knows v{codeVersion}; left untouched"));
                return false;
            }

            try
            {
                value = JsonUtility.FromJson<T>(entry.Json);
            }
            catch (ArgumentException exception)
            {
                Raise(new SaveError(SaveErrorKind.EntryCorrupt, $"'{key}' could not be read as {typeof(T).Name}", exception));
                value = default;
                return false;
            }

            if (value == null) return false;

            if (entry.Version < codeVersion && value is ISaveMigration migration)
            {
                migration.Migrate(entry.Version, entry.Json);
                value = (T)migration;
            }

            return true;
        }

        /// <summary>Raw text entry: no versioning, no JSON handling.</summary>
        public bool TryGetString(string key, out string value)
        {
            value = null;
            EnsureLoaded();
            if (key == null || !entries.TryGetValue(key, out var entry) || lockedKeys.Contains(key)) return false;
            value = entry.Json;
            return true;
        }

        public void Set<T>(string key, T value)
        {
            ThrowIfUnsupported(typeof(T));
            if (value == null) throw new ArgumentNullException(nameof(value), $"Use Remove to drop '{key}'.");
            Put(key, JsonUtility.ToJson(value), SaveVersionAttribute.Of(typeof(T)));
        }

        public void SetString(string key, string value)
        {
            Put(key, value ?? string.Empty, 0);
        }

        public bool Remove(string key)
        {
            ValidateKey(key);
            EnsureLoaded();
            if (IsLocked(key) || !entries.Remove(key)) return false;
            MarkDirty();
            return true;
        }

        /// <summary>
        /// Writes the save if it changed, or always with <paramref name="force"/> (which also records play time).
        /// False when nothing could be written - read-only, or the backend failed (the save stays dirty and
        /// autosave retries).
        /// </summary>
        public bool Flush(bool force = false)
        {
            EnsureLoaded();
            if (IsReadOnly || flushing) return false;
            if (!IsDirty && !metadataDirty && !force) return true;
            return WriteNow(true);
        }

        /// <summary>
        /// The save as it is on disk, ready to upload. Pending changes are written first, so the snapshot id
        /// always names content that exists locally. False when read-only or the write fails.
        /// </summary>
        public bool TryExportSnapshot(out SaveSnapshot snapshot)
        {
            snapshot = null;
            EnsureLoaded();
            if (IsReadOnly) return false;
            if ((IsDirty || SnapshotId == null) && !Flush(true)) return false;

            var data = BuildData(SnapshotId, Revision, LastModifiedUtc.Ticks, null);
            snapshot = new SaveSnapshot(JsonUtility.ToJson(data), data);
            return true;
        }

        /// <summary>
        /// Swaps the whole save for <paramref name="snapshot"/> and records it as what the cloud holds. Local
        /// changes not in it are dropped - deciding that is the caller's job. Refused when the local save is
        /// read-only or the snapshot is from a newer format.
        /// </summary>
        public bool ReplaceWith(SaveSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            EnsureLoaded();

            if (IsReadOnly)
            {
                Raise(new SaveError(SaveErrorKind.ReplaceRefused, $"The local save is read-only, not replaced with {snapshot}"));
                return false;
            }

            if (snapshot.FormatVersion > FormatVersion)
            {
                Raise(new SaveError(SaveErrorKind.NewerFormat,
                    $"Snapshot {snapshot} is format v{snapshot.FormatVersion}, this build knows v{FormatVersion}; not replaced"));
                return false;
            }

            Apply(snapshot.CopyData());
            lockedKeys.Clear();
            SyncedSnapshotId = snapshot.SnapshotId;
            IsDirty = false;
            metadataDirty = true;

            // No BeforeFlush: its subscribers would write their pre-swap state over the snapshot.
            if (!flushing) WriteNow(false);
            InvokeSafely(Replaced);
            return true;
        }

        /// <summary>Records that the cloud now holds <paramref name="snapshotId"/>; written with the next flush.</summary>
        public void MarkSynced(string snapshotId)
        {
            EnsureLoaded();
            if (SyncedSnapshotId == snapshotId) return;
            SyncedSnapshotId = snapshotId;
            MarkMetadataDirty();
        }

        /// <summary>
        /// Deletes the save from the backend and starts over, read-only state included. What the cloud was last
        /// known to hold is kept, so a cloud sync uploads the reset instead of restoring the old save.
        /// </summary>
        public void Clear()
        {
            try
            {
                backend.Delete();
            }
            catch (Exception exception)
            {
                Raise(new SaveError(SaveErrorKind.DeleteFailed, $"Could not delete the save at {backend}", exception));
            }

            entries.Clear();
            lockedKeys.Clear();
            description = null;
            PlayTime = 0;
            LastModifiedUtc = default;
            SnapshotId = null;
            Revision = 0;
            ReadOnlyReason = null;
            IsDirty = false;
            metadataDirty = false;
            IsLoaded = true;
            if (SyncedSnapshotId != null) MarkMetadataDirty();
            InvokeSafely(Cleared);
        }

        /// <summary>Advances play time and runs autosave. Time should be unscaled.</summary>
        public void Tick(float deltaTime)
        {
            if (!IsLoaded) return;
            PlayTime += deltaTime;
            if (!AutoSave || (!IsDirty && !metadataDirty) || IsReadOnly) return;

            dirtyAge += deltaTime;
            idleTime += deltaTime;
            if (idleTime < AutoSaveDelay && dirtyAge < AutoSaveMaxDelay) return;

            if (!Flush())
            {
                // Back off for a full delay instead of retrying a failing write every frame.
                dirtyAge = 0f;
                idleTime = 0f;
            }
        }

        private void Put(string key, string json, int version)
        {
            ValidateKey(key);
            EnsureLoaded();
            if (IsLocked(key)) return;
            if (entries.TryGetValue(key, out var existing) && existing.Version == version && existing.Json == json) return;

            entries[key] = new SaveEntry { Key = key, Version = version, Json = json };
            MarkDirty();
        }

        private bool IsLocked(string key)
        {
            if (!lockedKeys.Contains(key)) return false;
            Raise(new SaveError(SaveErrorKind.EntryLocked, $"'{key}' belongs to a newer version and is kept untouched"));
            return true;
        }

        private void MarkDirty()
        {
            if (!IsDirty && !metadataDirty) dirtyAge = 0f;
            IsDirty = true;
            idleTime = 0f;
        }

        private void MarkMetadataDirty()
        {
            if (!IsDirty && !metadataDirty) dirtyAge = 0f;
            metadataDirty = true;
            idleTime = 0f;
        }

        private bool WriteNow(bool notifyBeforeFlush)
        {
            flushing = true;
            try
            {
                if (notifyBeforeFlush) InvokeSafely(BeforeFlush);

                // Only a content change is a new snapshot; play time and sync bookkeeping ride along without one.
                var newSnapshot = IsDirty || SnapshotId == null;
                var data = newSnapshot
                    ? BuildData(Guid.NewGuid().ToString("N"), Revision + 1, DateTime.UtcNow.Ticks, SyncedSnapshotId)
                    : BuildData(SnapshotId, Revision, LastModifiedUtc.Ticks, SyncedSnapshotId);

                try
                {
                    backend.Write(JsonUtility.ToJson(data));
                }
                catch (Exception exception)
                {
                    Raise(new SaveError(SaveErrorKind.WriteFailed, $"Could not write the save to {backend}", exception));
                    return false;
                }

                SnapshotId = data.SnapshotId;
                Revision = data.Revision;
                LastModifiedUtc = new DateTime(data.ModifiedUtcTicks, DateTimeKind.Utc);
                IsDirty = false;
                metadataDirty = false;
                dirtyAge = 0f;
                idleTime = 0f;
            }
            finally
            {
                flushing = false;
            }

            InvokeSafely(Flushed);
            return true;
        }

        private SaveFileData BuildData(string snapshotId, long revision, long modifiedUtcTicks, string syncedSnapshotId)
        {
            return new SaveFileData
            {
                FormatVersion = FormatVersion,
                SnapshotId = snapshotId,
                Revision = revision,
                DeviceName = deviceName,
                PlayTimeTicks = (long)(PlayTime * TimeSpan.TicksPerSecond),
                ModifiedUtcTicks = modifiedUtcTicks,
                Description = description,
                SyncedSnapshotId = syncedSnapshotId,
                Entries = new List<SaveEntry>(entries.Values),
            };
        }

        private void EnsureLoaded()
        {
            if (!IsLoaded) Load();
        }

        private void Apply(SaveFileData data)
        {
            entries.Clear();
            foreach (var entry in data.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Key)) continue;
                entries[entry.Key] = entry;
            }

            PlayTime = data.PlayTimeTicks / (double)TimeSpan.TicksPerSecond;
            description = data.Description;
            LastModifiedUtc = new DateTime(data.ModifiedUtcTicks, DateTimeKind.Utc);
            SnapshotId = string.IsNullOrEmpty(data.SnapshotId) ? null : data.SnapshotId;
            Revision = data.Revision;
            SyncedSnapshotId = string.IsNullOrEmpty(data.SyncedSnapshotId) ? null : data.SyncedSnapshotId;

            if (data.FormatVersion > FormatVersion)
            {
                MakeReadOnly(SaveErrorKind.NewerFormat,
                    $"The save at {backend} is format v{data.FormatVersion}, this build knows v{FormatVersion}");
            }
        }

        private void MakeReadOnly(SaveErrorKind kind, string message, Exception exception = null)
        {
            ReadOnlyReason = message;
            Raise(new SaveError(kind, message + "; saving is disabled", exception));
        }

        private void Raise(SaveError error)
        {
            LastError = error;
            Debug.LogError($"[Save] {error}");

            if (ErrorRaised == null) return;
            foreach (var handler in ErrorRaised.GetInvocationList())
            {
                try
                {
                    ((Action<SaveError>)handler)(error);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void InvokeSafely(Action action)
        {
            if (action == null) return;
            foreach (var handler in action.GetInvocationList())
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Save key is empty.", nameof(key));
        }

        private static void ThrowIfUnsupported(Type type)
        {
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type.IsArray ||
                typeof(IEnumerable).IsAssignableFrom(type) || typeof(Object).IsAssignableFrom(type))
            {
                throw new ArgumentException(
                    $"{type.Name} can't be a save entry: JsonUtility only round-trips [Serializable] classes and " +
                    "structs at the top level. Wrap it in one, or use SetString.");
            }
        }
    }
}
