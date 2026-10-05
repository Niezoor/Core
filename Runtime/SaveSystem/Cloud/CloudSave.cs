using System;
using System.Threading;
using UnityEngine;

namespace Core.SaveSystem.Cloud
{
    /// <summary>
    /// Cloud sync for <see cref="Save"/>. A platform module (or the game) registers a provider at boot; from then on
    /// it syncs on its own: once at start, after local writes no more often than <see cref="MinSyncInterval"/>,
    /// right away when the app is paused, and with growing back-off while the service is unreachable. A conflict
    /// stops it until the game calls <see cref="ResolveConflictAsync"/> - subscribe to
    /// <see cref="ConflictDetected"/>, or check <see cref="PendingConflict"/> when the UI is ready.
    /// </summary>
    public static class CloudSave
    {
        public static event Action<CloudConflict> ConflictDetected;
        public static event Action<CloudSyncResult> SyncFinished;
        /// <summary>Raised before <see cref="SyncFinished"/>, so its handlers already see the new status.</summary>
        public static event Action<CloudStatus> StatusChanged;

        public static bool AutoSync { get; set; } = true;
        /// <summary>Seconds between automatic uploads while the save keeps changing.</summary>
        public static float MinSyncInterval { get; set; } = 60f;
        /// <summary>First retry after a failed sync; doubles up to <see cref="MaxRetryDelay"/>.</summary>
        public static float RetryDelay { get; set; } = 30f;
        public static float MaxRetryDelay { get; set; } = 600f;

        public static ICloudSaveProvider Provider { get; private set; }
        public static bool IsEnabled => Provider != null;
        public static CloudStatus Status { get; private set; }
        public static bool IsSyncing => sync?.IsSyncing ?? false;
        public static CloudConflict PendingConflict => sync?.PendingConflict;
        public static CloudSyncResult? LastResult => sync?.LastResult;
        public static string LastError => sync?.LastError;
        public static DateTime LastSuccessUtc => sync?.LastSuccessUtc ?? default;

        private static int providerPriority;
        private static CloudSync sync;
        private static CloudSaveRunner runner;
        private static float? dueAt;
        private static float lastAttemptAt = float.NegativeInfinity;
        private static float currentRetryDelay;

        private static CloudSync Sync
        {
            get
            {
                if (sync != null || Provider == null) return sync;

                sync = new CloudSync(Save.Store, Provider);
                sync.ConflictDetected += conflict =>
                {
                    SetStatus(CloudStatus.Conflict);
                    ConflictDetected?.Invoke(conflict);
                };
                sync.SyncFinished += result =>
                {
                    SetStatus(StatusAfter(result));
                    SyncFinished?.Invoke(result);
                };
                Save.Flushed += OnLocalSaveWritten;
                return sync;
            }
        }

        /// <summary>
        /// Offers a provider; the highest <paramref name="priority"/> wins, so a game can override what a platform
        /// module registered. Only before the first sync - call it from a
        /// <c>RuntimeInitializeOnLoadMethod(BeforeSceneLoad)</c>.
        /// </summary>
        public static void Register(ICloudSaveProvider provider, int priority = 0)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (sync != null)
            {
                Debug.LogError($"[CloudSave] {provider.Name} registered after cloud sync started; ignored.");
                return;
            }

            if (Provider != null && priority <= providerPriority) return;

            Provider = provider;
            providerPriority = priority;
            dueAt = Time.realtimeSinceStartup;
            if (!runner && Application.isPlaying) runner = CloudSaveRunner.Create();
            SetStatus(CloudStatus.Loading);
        }

        public static async Awaitable<CloudSyncResult> SyncAsync(CancellationToken cancellationToken = default)
        {
            if (Sync == null) return CloudSyncResult.Skipped;

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Application.exitCancellationToken);
            var result = await Sync.SyncAsync(linked.Token);
            Reschedule(result);
            return result;
        }

        /// <inheritdoc cref="CloudSync.ResolveConflictAsync"/>
        public static async Awaitable<CloudSyncResult> ResolveConflictAsync(CloudConflictChoice choice,
            CancellationToken cancellationToken = default)
        {
            if (Sync == null) return CloudSyncResult.Skipped;

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Application.exitCancellationToken);
            var result = await Sync.ResolveConflictAsync(choice, linked.Token);
            Reschedule(result);
            return result;
        }

        internal static void Tick()
        {
            if (!AutoSync || dueAt == null || Time.realtimeSinceStartup < dueAt) return;
            if (Sync == null || sync.IsSyncing || sync.PendingConflict != null) return;

            dueAt = null;
            RunInBackground();
        }

        internal static void OnApplicationSuspending()
        {
            if (!AutoSync || Sync == null || sync.IsSyncing || sync.PendingConflict != null) return;
            if (Save.HasUnsyncedChanges) RunInBackground();
        }

        private static async void RunInBackground()
        {
            try
            {
                await SyncAsync();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void Reschedule(CloudSyncResult result)
        {
            var now = Time.realtimeSinceStartup;
            lastAttemptAt = now;

            switch (result)
            {
                case CloudSyncResult.Unavailable:
                case CloudSyncResult.Failed:
                    currentRetryDelay = currentRetryDelay <= 0f ? RetryDelay : Mathf.Min(currentRetryDelay * 2f, MaxRetryDelay);
                    dueAt = now + currentRetryDelay;
                    break;
                case CloudSyncResult.Conflict:
                case CloudSyncResult.Skipped:
                    break;
                default:
                    currentRetryDelay = 0f;
                    dueAt = Save.HasUnsyncedChanges ? now + MinSyncInterval : null;
                    break;
            }
        }

        private static CloudStatus StatusAfter(CloudSyncResult result)
        {
            switch (result)
            {
                case CloudSyncResult.UpToDate:
                case CloudSyncResult.Uploaded:
                case CloudSyncResult.Downloaded:
                    return CloudStatus.Synced;
                case CloudSyncResult.Conflict:
                    return CloudStatus.Conflict;
                case CloudSyncResult.Unavailable:
                    return CloudStatus.Offline;
                case CloudSyncResult.Failed:
                    return CloudStatus.Error;
                default:
                    // Skipped only reports when the local save is read-only, which no later sync will fix.
                    return sync.LastError != null ? CloudStatus.Error : Status;
            }
        }

        private static void SetStatus(CloudStatus status)
        {
            if (Status == status) return;
            Status = status;
            StatusChanged?.Invoke(status);
        }

        private static void OnLocalSaveWritten()
        {
            if (dueAt != null || sync == null || sync.PendingConflict != null || !Save.HasUnsyncedChanges) return;
            dueAt = Mathf.Max(Time.realtimeSinceStartup, lastAttemptAt + MinSyncInterval);
        }

        /// <summary>Forgets the provider and sync state; the next <see cref="Register"/> starts over.</summary>
        internal static void ResetForTests()
        {
            if (runner)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(runner.gameObject);
                else UnityEngine.Object.DestroyImmediate(runner.gameObject);
            }

            ResetStatics();
        }

        // Domain reload is off in Enter Play Mode settings, so nothing static may survive into the next session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (sync != null) Save.Flushed -= OnLocalSaveWritten;
            ConflictDetected = null;
            SyncFinished = null;
            StatusChanged = null;
            Status = CloudStatus.Disabled;
            AutoSync = true;
            MinSyncInterval = 60f;
            RetryDelay = 30f;
            MaxRetryDelay = 600f;
            Provider = null;
            providerPriority = 0;
            sync = null;
            runner = null;
            dueAt = null;
            lastAttemptAt = float.NegativeInfinity;
            currentRetryDelay = 0f;
        }
    }
}
