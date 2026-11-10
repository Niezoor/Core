using System.Collections.Generic;
using Core.Utilities;
using Core.Utilities.Settings;
using UnityEngine;

namespace Core.Bootstrap
{
    /// <summary>How <see cref="Boot"/> starts the game, in Project Settings &gt; Core &gt; Boot. Preloaded, so it is there
    /// before anything else loads. Its steps come from the features themselves (<see cref="BootStepAttribute"/>).</summary>
    [PreloadedSettings, SettingsMenu("Core/Boot")]
    public sealed class BootSettings : SettingsAsset<BootSettings>
    {
        [Tooltip("Loaded in the background during the splash and activated when it ends.")]
        public SceneReference EntryScene = new();

#pragma warning disable CS0618
        [Tooltip("Legacy: read only while Entry Scene is empty, and moved into it when the asset is edited.")]
        [HideInInspector] public SceneRef FirstScene;

        /// <summary><see cref="EntryScene"/>, or the legacy <see cref="FirstScene"/> while it is still empty.</summary>
        public SceneReference StartScene => EntryScene.IsSet || !FirstScene ? EntryScene : FirstScene.ToReference();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (EntryScene.IsSet || !FirstScene) return;
            EntryScene = FirstScene.ToReference();
            FirstScene = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
#pragma warning restore CS0618

        [Tooltip("Shortest time the splash stays on screen when nobody taps (Boot.Continue skips the rest).")]
        [Min(0f)] public float MinSplashDuration = 2f;

        [Tooltip("Leave the splash on its own once loaded and MinSplashDuration passed; off = wait for Boot.Continue.")]
        public bool AutoContinue = true;

        [Tooltip("Initializers taking longer than this in total log a warning - they delay every scene start.")]
        [Min(0f)] public float InitializerBudgetMs = 50f;

        // Filled by the editor from [BootStep] types and kept sorted by their order (BootStepSync); the project only
        // switches steps on and off, sets their fields and adds steps that don't add themselves.
        [Header("Steps - added by [BootStep], switch off rather than remove")]
        [SerializeReference, SubclassPicker] public List<BootInitializer> Initializers = new();
        [SerializeReference, SubclassPicker] public List<BootService> Services = new();
        [SerializeReference, SubclassPicker] public List<SplashTask> SplashTasks = new();
    }
}
