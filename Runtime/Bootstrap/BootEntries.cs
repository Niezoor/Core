using System;
using System.Threading;
using UnityEngine;

namespace Core.Bootstrap
{
    [Flags]
    public enum BootPlatforms
    {
        None = 0,
        Editor = 1 << 0,
        Windows = 1 << 1,
        MacOS = 1 << 2,
        Linux = 1 << 3,
        WebGL = 1 << 4,
        Android = 1 << 5,
        iOS = 1 << 6,
        Standalone = Windows | MacOS | Linux,
        Mobile = Android | iOS,
        All = Editor | Standalone | WebGL | Mobile,
    }

    public static class BootPlatformsExtensions
    {
        /// <summary>The running platform; <see cref="BootPlatforms.None"/> on one not listed.</summary>
        public static BootPlatforms Current
        {
            get
            {
#if UNITY_EDITOR
                return BootPlatforms.Editor;
#elif UNITY_STANDALONE_WIN
                return BootPlatforms.Windows;
#elif UNITY_STANDALONE_OSX
                return BootPlatforms.MacOS;
#elif UNITY_STANDALONE_LINUX
                return BootPlatforms.Linux;
#elif UNITY_WEBGL
                return BootPlatforms.WebGL;
#elif UNITY_ANDROID
                return BootPlatforms.Android;
#elif UNITY_IOS
                return BootPlatforms.iOS;
#else
                return BootPlatforms.None;
#endif
            }
        }

        public static bool Includes(this BootPlatforms platforms, BootPlatforms platform) =>
            platforms == BootPlatforms.All || (platforms & platform) != 0;
    }

    /// <summary>What every entry in <see cref="BootSettings"/> has: a switch and the platforms it runs on.</summary>
    [Serializable]
    public abstract class BootEntry
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private BootPlatforms platforms = BootPlatforms.All;

        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        public BootPlatforms Platforms
        {
            get => platforms;
            set => platforms = value;
        }

        public bool IsActive => enabled && platforms.Includes(BootPlatformsExtensions.Current);

        public virtual string DisplayName => GetType().Name;

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Runs synchronously before the first scene's Awake, in every scene the game starts from - the editor's
    /// gameplay scenes included - so everything here must take milliseconds and work offline on every platform:
    /// the local save, preloaded settings, applying the player's options. No Addressables or Localization here;
    /// they can't load synchronously on WebGL.
    /// </summary>
    [Serializable]
    public abstract class BootInitializer : BootEntry
    {
        public abstract void Initialize();
    }

    /// <summary>
    /// Started right after the initializers and never waited on: sign-in, cloud sync, store, ads, analytics. It
    /// reports its own state (an IsReady property, events) for the game to check when it needs the service.
    /// </summary>
    [Serializable]
    public abstract class BootService : BootEntry
    {
        /// <param name="appLifetime">Cancelled when the application quits.</param>
        public abstract void Start(CancellationToken appLifetime);
    }

    /// <summary>
    /// Async loading that runs only while the boot scene (the splash) is on screen, in list order. The game must never rely
    /// on it having run - playing a gameplay scene in the editor skips the splash.
    /// </summary>
    [Serializable]
    public abstract class SplashTask : BootEntry
    {
        [Tooltip("A failure stops the splash with Retry; otherwise it is logged and the splash goes on.")]
        [SerializeField] private bool required = true;
        [Tooltip("Seconds, 0 = none. Only a task that awaits can be stopped.")]
        [SerializeField, Min(0f)] private float timeout;

        public bool Required
        {
            get => required;
            set => required = value;
        }

        public float Timeout
        {
            get => timeout;
            set => timeout = value;
        }

        public abstract Awaitable RunAsync(SplashContext context, CancellationToken cancellationToken);
    }

    /// <summary>What a running <see cref="SplashTask"/> tells the splash.</summary>
    public sealed class SplashContext
    {
        private readonly Action<float> reportProgress;
        private readonly Action<string> reportStatus;

        internal SplashContext(Action<float> reportProgress, Action<string> reportStatus)
        {
            this.reportProgress = reportProgress;
            this.reportStatus = reportStatus;
        }

        /// <param name="progress">0..1 within this task.</param>
        public void ReportProgress(float progress) => reportProgress(Mathf.Clamp01(progress));

        /// <param name="textKey">Localization key the splash may show, e.g. "core.boot.downloading".</param>
        public void ReportStatus(string textKey) => reportStatus(textKey);
    }
}
