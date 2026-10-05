using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Core.Utilities.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using ThreadPriority = UnityEngine.ThreadPriority;

namespace Core.Bootstrap
{
    public enum SplashState
    {
        /// <summary>Not in the boot scene, or the splash already handed over to the first scene.</summary>
        None,
        Loading,
        /// <summary>Everything loaded; leaving on <see cref="Boot.Continue"/> or after the minimum duration.</summary>
        Ready,
        /// <summary>A required task failed; <see cref="Boot.Retry"/> resumes from it.</summary>
        Failed,
    }

    /// <summary>
    /// Starts the game from <see cref="BootSettings"/> in three phases:
    /// <list type="number">
    /// <item>initializers - synchronously before the first scene's Awake, whichever scene that is;</item>
    /// <item>services - started in the background, never waited on;</item>
    /// <item>splash tasks - only in the scene holding <see cref="BootScene"/>, then the first scene is activated.</item>
    /// </list>
    /// The boot scene is the game's own: an animation, a full-screen button calling <see cref="Continue"/>,
    /// optionally a progress view - plus the <see cref="BootScene"/> marker, which the editor keeps first in Build
    /// Settings.
    /// </summary>
    public static class Boot
    {
        /// <summary>Build index of the boot scene - 0 unless it was opened from outside the build list.</summary>
        public static int BootSceneIndex { get; private set; }

        /// <summary>Before <see cref="Restart"/> reloads the boot scene - the moment to kill tweens, clear pools.</summary>
        public static event Action Restarting;
        public static event Action<SplashState> SplashStateChanged;
        /// <summary>0..1 across every splash task, the first scene's loading included.</summary>
        public static event Action<float> SplashProgressChanged;

        public static bool IsInitialized { get; private set; }
        public static BootSettings Settings { get; private set; }
        public static SplashState SplashState { get; private set; }
        public static float SplashProgress { get; private set; }
        /// <summary>Text key a running task reported; null when it reported none.</summary>
        public static string SplashStatus { get; private set; }
        public static SplashTask FailedTask { get; private set; }
        public static Exception Failure { get; private set; }
        /// <summary>The splash on screen comes from <see cref="Restart"/>, not from launching the game.</summary>
        public static bool IsRestart { get; private set; }

        /// <summary>
        /// Every step run so far, oldest first: initializers and services once, splash tasks of the latest splash
        /// (a retry adds another run of the failed task). For the editor's Boot Monitor and for diagnostics.
        /// </summary>
        public static IReadOnlyList<BootStepRecord> Records => records;
        /// <summary><see cref="Time.realtimeSinceStartupAsDouble"/> of the splash's milestones; NaN until reached.</summary>
        public static double SplashStartedAt { get; private set; } = double.NaN;
        public static double SplashReadyAt { get; private set; } = double.NaN;
        public static double SplashLeftAt { get; private set; } = double.NaN;

        private static readonly List<BootStepRecord> records = new();
        private static readonly List<SplashTask> splashTasks = new();
        private static CancellationTokenSource splashCancellation;
        private static int splashSceneHandle = -1;
        private static int resumeIndex;
        private static float splashStartedAt;
        private static bool continueRequested;
        private static ThreadPriority previousLoadingPriority;
        private static bool loadingPriorityLowered;
        private static FirstSceneTask firstScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnLoad()
        {
            if (!BootSettings.TryGet(out var settings)) return;

            Initialize(settings);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>From <see cref="BootScene"/>: the boot scene is up, start (or restart) the splash in it.</summary>
        internal static void OnBootSceneAwake(Scene scene)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[Boot] The scene has a BootScene but the project has no BootSettings " +
                                 "(Project Settings > Core > Boot); no splash runs.");
                return;
            }

            if (scene.buildIndex >= 0) BootSceneIndex = scene.buildIndex;
            if (scene.handle != splashSceneHandle) StartSplash(scene.handle);
        }

        internal static void Initialize(BootSettings settings)
        {
            if (IsInitialized) return;
            IsInitialized = true;
            Settings = settings;

            var startedAt = Time.realtimeSinceStartupAsDouble;
            Debug.Log($"[Boot] Starting at {startedAt:0.000} s since launch");
            // Nothing async has loaded this early: the editor reports reads of async settings it would resolve anyway.
            SettingsRegistry.PreloadedOnly = true;
            try
            {
                RunInitializers(settings.Initializers, settings.InitializerBudgetMs);
                StartServices(settings.Services);
            }
            finally
            {
                SettingsRegistry.PreloadedOnly = false;
            }

            Debug.Log($"[Boot] Initialized in {(Time.realtimeSinceStartupAsDouble - startedAt) * 1000.0:0.0} ms " +
                      $"({CountRecords(BootPhase.Initializer)} initializers, {CountRecords(BootPhase.Service)} services started)");
        }

        private static int CountRecords(BootPhase phase)
        {
            var count = 0;
            foreach (var record in records)
            {
                if (record.Phase == phase) count++;
            }

            return count;
        }

        /// <summary>
        /// The splash's tap: leave for the first scene as soon as it is loaded, without waiting out
        /// <see cref="BootSettings.MinSplashDuration"/>. A tap while still loading is remembered.
        /// </summary>
        public static void Continue()
        {
            if (SplashState is SplashState.Loading or SplashState.Ready) continueRequested = true;
        }

        /// <summary>After <see cref="SplashState.Failed"/>: runs the splash again from the task that failed.</summary>
        public static void Retry()
        {
            if (SplashState != SplashState.Failed) return;
            FailedTask = null;
            Failure = null;
            RunSplash();
        }

        /// <summary>Back to the boot scene and through the splash again. Initializers and services keep running.</summary>
        public static void Restart()
        {
            foreach (var handler in Restarting?.GetInvocationList() ?? Array.Empty<Delegate>())
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

            StopSplash();
            IsRestart = true;
            SceneManager.LoadScene(BootSceneIndex);
        }

        private static void RunInitializers(List<BootInitializer> initializers, float budgetMs)
        {
            var total = Stopwatch.StartNew();
            var report = new StringBuilder();
            foreach (var initializer in initializers)
            {
                if (initializer == null || !initializer.IsActive) continue;

                var record = Record(BootPhase.Initializer, initializer.DisplayName);
                try
                {
                    initializer.Initialize();
                    record.Finish(BootStepResult.Succeeded);
                }
                catch (Exception exception)
                {
                    record.Finish(BootStepResult.Failed, exception.Message);
                    Debug.LogException(exception);
                }

                report.Append($"\n  {initializer.DisplayName}: {record.Duration * 1000.0:0.0} ms");
            }

            if (total.Elapsed.TotalMilliseconds > budgetMs)
            {
                Debug.LogWarning($"[Boot] Initializers took {total.Elapsed.TotalMilliseconds:0.0} ms, over the " +
                                 $"{budgetMs:0} ms budget - every scene start waits for them:{report}");
            }
        }

        private static void StartServices(List<BootService> services)
        {
            foreach (var service in services)
            {
                if (service == null || !service.IsActive) continue;

                // Only the synchronous start is timed - what the service does in the background is its own.
                var record = Record(BootPhase.Service, service.DisplayName);
                try
                {
                    service.Start(Application.exitCancellationToken);
                    record.Finish(BootStepResult.Succeeded);
                }
                catch (Exception exception)
                {
                    record.Finish(BootStepResult.Failed, exception.Message);
                    Debug.LogException(exception);
                }
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // The boot scene starts the splash from BootScene.Awake, before this runs for it. Any other scene replacing
            // it means the game left the splash some other way, e.g. by loading a scene itself.
            if (mode == LoadSceneMode.Single && scene.handle != splashSceneHandle) StopSplash();
        }

        internal static void StartSplash(int sceneHandle)
        {
            StopSplash();
            splashSceneHandle = sceneHandle;
            splashStartedAt = Time.realtimeSinceStartup;
            SplashStartedAt = Time.realtimeSinceStartupAsDouble;
            SplashReadyAt = double.NaN;
            SplashLeftAt = double.NaN;
            records.RemoveAll(r => r.Phase == BootPhase.Splash);
            continueRequested = false;
            resumeIndex = 0;
            FailedTask = null;
            Failure = null;

            splashTasks.Clear();
            foreach (var task in Settings.SplashTasks)
            {
                if (task != null && task.IsActive) splashTasks.Add(task);
            }

            // Last on purpose: a scene loaded without activation holds back every async load queued after it.
            firstScene = new FirstSceneTask(Settings.FirstScene);
            splashTasks.Add(firstScene);

            previousLoadingPriority = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = ThreadPriority.Low;
            loadingPriorityLowered = true;

            SetProgress(0f);
            RunSplash();
        }

        private static void StopSplash()
        {
            // RunSplash owns the source and disposes it once it has seen the cancellation.
            splashCancellation?.Cancel();
            splashCancellation = null;
            splashSceneHandle = -1;
            RestoreLoadingPriority();
            SetState(SplashState.None);
        }

        private static async void RunSplash()
        {
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Application.exitCancellationToken);
            splashCancellation = cancellation;
            var cancellationToken = cancellation.Token;
            SetState(SplashState.Loading);

            try
            {
                if (!await RunTasksAsync(cancellationToken)) return;

                SplashReadyAt = Time.realtimeSinceStartupAsDouble;
                Debug.Log($"[Boot] Splash loaded in {SplashReadyAt - SplashStartedAt:0.00} s:{SplashReport()}");
                SetState(SplashState.Ready);
                while (!continueRequested &&
                       !(Settings.AutoContinue && Time.realtimeSinceStartup - splashStartedAt >= Settings.MinSplashDuration))
                {
                    await Awaitable.NextFrameAsync(cancellationToken);
                }

                SplashLeftAt = Time.realtimeSinceStartupAsDouble;
                Debug.Log($"[Boot] Finished at {SplashLeftAt:0.00} s since launch - splash on screen " +
                          $"{SplashLeftAt - SplashStartedAt:0.00} s ({(continueRequested ? "continued by the player" : "minimum duration")}), " +
                          $"entering {(Settings.FirstScene ? Settings.FirstScene.name : "nothing")}");
                RestoreLoadingPriority();
                splashSceneHandle = -1;
                SetState(SplashState.None);
                firstScene.Activate();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (splashCancellation == cancellation) splashCancellation = null;
                cancellation.Dispose();
            }
        }

        /// <returns>False when a required task failed and the splash waits for <see cref="Retry"/>.</returns>
        private static async Awaitable<bool> RunTasksAsync(CancellationToken cancellationToken)
        {
            var count = splashTasks.Count;
            for (var i = resumeIndex; i < count; i++)
            {
                var task = splashTasks[i];
                var index = i;
                SplashStatus = null;
                var record = Record(BootPhase.Splash, task.DisplayName);
                var context = new SplashContext(
                    p =>
                    {
                        record.Progress = p;
                        SetProgress((index + p) / count);
                    },
                    s => SplashStatus = record.Status = s);

                try
                {
                    await RunWithTimeoutAsync(task, context, cancellationToken);
                    record.Finish(BootStepResult.Succeeded);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    record.Finish(BootStepResult.Cancelled);
                    throw;
                }
                catch (Exception exception)
                {
                    var timedOut = exception is TimeoutException;
                    if (task.Required)
                    {
                        record.Finish(timedOut ? BootStepResult.TimedOut : BootStepResult.Failed, exception.Message);
                        resumeIndex = i;
                        FailedTask = task;
                        Failure = exception;
                        Debug.LogError($"[Boot] {task.DisplayName} failed: {exception}");
                        SetState(SplashState.Failed);
                        return false;
                    }

                    record.Finish(timedOut ? BootStepResult.TimedOut : BootStepResult.Skipped, exception.Message);
                    Debug.LogWarning($"[Boot] {task.DisplayName} failed, going on without it: {exception}");
                }

                SetProgress((float)(i + 1) / count);
            }

            resumeIndex = count;
            SplashStatus = null;
            return true;
        }

        private static async Awaitable RunWithTimeoutAsync(SplashTask task, SplashContext context,
            CancellationToken cancellationToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var awaiter = task.RunAsync(context, linked.Token).GetAwaiter();

            // Polled rather than CancelAfter: timers need threads, which WebGL lacks.
            var deadline = task.Timeout > 0f ? Time.realtimeSinceStartup + task.Timeout : float.PositiveInfinity;
            while (!awaiter.IsCompleted)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    linked.Cancel();
                    throw new TimeoutException($"{task.DisplayName} did not finish within {task.Timeout:0.#} s");
                }

                await Awaitable.NextFrameAsync(cancellationToken);
            }

            awaiter.GetResult();
        }

        private static string SplashReport()
        {
            var report = new StringBuilder();
            foreach (var record in records)
            {
                if (record.Phase != BootPhase.Splash) continue;
                report.Append($"\n  {record.Name}: {record.Duration * 1000.0:0} ms");
                if (record.Result != BootStepResult.Succeeded) report.Append($" ({record.Result})");
            }

            return report.ToString();
        }

        private static BootStepRecord Record(BootPhase phase, string name)
        {
            var record = new BootStepRecord(phase, name);
            records.Add(record);
            return record;
        }

        private static void SetState(SplashState state)
        {
            if (SplashState == state) return;
            SplashState = state;
            SplashStateChanged?.Invoke(state);
        }

        private static void SetProgress(float progress)
        {
            if (Mathf.Approximately(SplashProgress, progress)) return;
            SplashProgress = progress;
            SplashProgressChanged?.Invoke(progress);
        }

        private static void RestoreLoadingPriority()
        {
            if (!loadingPriorityLowered) return;
            Application.backgroundLoadingPriority = previousLoadingPriority;
            loadingPriorityLowered = false;
        }

        /// <summary>Starts over with <paramref name="settings"/>, without hooking scene loads.</summary>
        internal static void ResetForTests(BootSettings settings)
        {
            ResetStatics();
            if (settings != null) Initialize(settings);
        }

        // Domain reload is off in Enter Play Mode settings, so nothing static may survive into the next session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            splashCancellation?.Cancel();
            splashCancellation = null;
            RestoreLoadingPriority();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            // Cleared on the next Play, not on exit, so the Boot Monitor still shows the last run afterwards.
            records.Clear();
            SplashStartedAt = double.NaN;
            SplashReadyAt = double.NaN;
            SplashLeftAt = double.NaN;
            Restarting = null;
            SplashStateChanged = null;
            SplashProgressChanged = null;
            IsInitialized = false;
            Settings = null;
            SplashState = SplashState.None;
            SplashProgress = 0f;
            SplashStatus = null;
            FailedTask = null;
            Failure = null;
            IsRestart = false;
            splashTasks.Clear();
            splashSceneHandle = -1;
            resumeIndex = 0;
            continueRequested = false;
            firstScene = null;
        }
    }
}
