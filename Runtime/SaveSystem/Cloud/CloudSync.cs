using System;
using System.Threading;
using UnityEngine;

namespace Core.SaveSystem.Cloud
{
    /// <summary>
    /// Keeps a <see cref="SaveStore"/> and one cloud provider in step. The local save stays the source of truth and
    /// the game never waits on this. A sync compares snapshot ids against the one the cloud was last known to hold:
    /// <list type="bullet">
    /// <item>same snapshot on both sides - nothing to do;</item>
    /// <item>cloud unchanged since the last sync - the local save is newer and is uploaded;</item>
    /// <item>cloud changed since - the player decides (<see cref="PendingConflict"/>), nothing moves until then.</item>
    /// </list>
    /// The only automatic download is onto a blank install that has never synced.
    /// </summary>
    public sealed class CloudSync
    {
        public event Action<CloudConflict> ConflictDetected;
        public event Action<CloudSyncResult> SyncFinished;

        public ICloudSaveProvider Provider { get; }
        public bool IsSyncing { get; private set; }
        public CloudConflict PendingConflict { get; private set; }
        public CloudSyncResult? LastResult { get; private set; }
        public string LastError { get; private set; }
        public DateTime LastSuccessUtc { get; private set; }

        private readonly SaveStore store;

        public CloudSync(SaveStore store, ICloudSaveProvider provider)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public async Awaitable<CloudSyncResult> SyncAsync(CancellationToken cancellationToken = default)
        {
            if (IsSyncing) return CloudSyncResult.Skipped;
            if (PendingConflict != null) return CloudSyncResult.Conflict;
            if (store.IsReadOnly) return Finish(CloudSyncResult.Skipped, "The local save is read-only");

            IsSyncing = true;
            try
            {
                return await RunAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return Finish(CloudSyncResult.Failed, $"{Provider.Name}: {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                IsSyncing = false;
            }
        }

        /// <summary>
        /// Settles <see cref="PendingConflict"/>. Keeping the local save treats the cloud's version as seen, so it
        /// is uploaded as the newer one - and retried without asking again if the upload fails.
        /// </summary>
        public async Awaitable<CloudSyncResult> ResolveConflictAsync(CloudConflictChoice choice,
            CancellationToken cancellationToken = default)
        {
            var conflict = PendingConflict;
            if (conflict == null || IsSyncing) return CloudSyncResult.Skipped;
            PendingConflict = null;

            if (choice == CloudConflictChoice.KeepCloud) return Download(conflict.Cloud);

            store.MarkSynced(conflict.Cloud.SnapshotId);
            return await SyncAsync(cancellationToken);
        }

        private async Awaitable<CloudSyncResult> RunAsync(CancellationToken cancellationToken)
        {
            if (!Provider.IsAvailable && !await Provider.SignInAsync(cancellationToken))
            {
                return Finish(CloudSyncResult.Unavailable, $"{Provider.Name} is not available");
            }

            var text = await Provider.DownloadAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            SaveSnapshot remote = null;
            if (text != null && !SaveSnapshot.TryParse(text, out remote))
            {
                return Finish(CloudSyncResult.Failed, $"The save in {Provider.Name} is unreadable; left untouched");
            }

            if (remote != null && remote.FormatVersion > SaveStore.FormatVersion)
            {
                return Finish(CloudSyncResult.Failed,
                    $"The save in {Provider.Name} is from a newer version of the game (format v{remote.FormatVersion})");
            }

            var neverSyncedBlank = store.SnapshotId == null && store.Keys.Count == 0 && store.SyncedSnapshotId == null;
            if (remote != null && neverSyncedBlank) return Download(remote);

            if (!store.TryExportSnapshot(out var local))
            {
                return Finish(CloudSyncResult.Failed, "The local save could not be written");
            }

            if (remote != null && remote.SnapshotId == local.SnapshotId)
            {
                store.MarkSynced(local.SnapshotId);
                return Finish(CloudSyncResult.UpToDate);
            }

            if (remote == null || remote.SnapshotId == store.SyncedSnapshotId)
            {
                await Provider.UploadAsync(local, cancellationToken);
                store.MarkSynced(local.SnapshotId);
                return Finish(CloudSyncResult.Uploaded);
            }

            PendingConflict = new CloudConflict(local, remote, local.SnapshotId != store.SyncedSnapshotId);
            Notify(ConflictDetected, PendingConflict);
            return Finish(CloudSyncResult.Conflict);
        }

        private CloudSyncResult Download(SaveSnapshot remote)
        {
            return store.ReplaceWith(remote)
                ? Finish(CloudSyncResult.Downloaded)
                : Finish(CloudSyncResult.Failed, $"The local save refused the snapshot from {Provider.Name}");
        }

        private CloudSyncResult Finish(CloudSyncResult result, string error = null)
        {
            LastResult = result;
            LastError = error;
            if (error != null) Debug.LogWarning($"[CloudSave] {result}: {error}");
            if (result is CloudSyncResult.UpToDate or CloudSyncResult.Uploaded or CloudSyncResult.Downloaded)
            {
                LastSuccessUtc = DateTime.UtcNow;
            }

            Notify(SyncFinished, result);
            return result;
        }

        private static void Notify<T>(Action<T> handlers, T value)
        {
            if (handlers == null) return;
            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<T>)handler)(value);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
