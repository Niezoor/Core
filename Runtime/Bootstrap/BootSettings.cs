using System.Collections.Generic;
using Core.Utilities;
using Core.Utilities.Settings;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.Bootstrap
{
    /// <summary>What <see cref="Boot"/> runs, in Project Settings &gt; Core &gt; Boot. Preloaded, so it is there before
    /// anything else loads.</summary>
    public sealed class BootSettings : ScriptableObjectPreloadedSettings<BootSettings>
    {
        [Tooltip("Loaded in the background during the splash and activated when it ends.")]
        public SceneRef FirstScene;

        [Tooltip("Shortest time the splash stays on screen when nobody taps (Boot.Continue skips the rest).")]
        [Min(0f)] public float MinSplashDuration = 2f;

        [Tooltip("Leave the splash on its own once loaded and MinSplashDuration passed; off = wait for Boot.Continue.")]
        public bool AutoContinue = true;

        [Tooltip("Initializers taking longer than this in total log a warning - they delay every scene start.")]
        [Min(0f)] public float InitializerBudgetMs = 50f;

        [SerializeReference, SubclassPicker] public List<BootInitializer> Initializers = new();
        [SerializeReference, SubclassPicker] public List<BootService> Services = new();
        [SerializeReference, SubclassPicker] public List<SplashTask> SplashTasks = new();

#if UNITY_EDITOR
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider() => GetDefaultSettings("Core/Boot");
#endif
    }
}
