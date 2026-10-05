using UnityEngine;

namespace Core.Utilities.Settings
{
    /// <summary>
    /// Project-wide settings: one asset per type, found by its type - the file can be renamed and moved freely.
    /// <list type="bullet">
    /// <item>Default: in a build loaded from Addressables by <see cref="SettingsRegistry.LoadAllAsync"/> (the splash's
    /// <c>LoadSettingsTask</c>), so it costs nothing at launch.</item>
    /// <item><see cref="PreloadedSettingsAttribute">[PreloadedSettings]</see>: in memory from launch, readable even in
    /// <c>BeforeSceneLoad</c>, but it and everything it references delay the start.</item>
    /// </list>
    /// In the editor every type is there synchronously; the asset is created on first use, and the editor keeps
    /// Preloaded Assets and the Addressables group in step with the attribute.
    /// Show it in Project Settings with <see cref="SettingsMenuAttribute">[SettingsMenu]</see>.
    /// </summary>
    public abstract class SettingsAsset : ScriptableObject
    {
    }

    /// <inheritdoc cref="SettingsAsset"/>
    public abstract class SettingsAsset<T> : SettingsAsset where T : SettingsAsset<T>
    {
        /// <summary>
        /// The settings. In a build an async type throws until <see cref="SettingsRegistry.LoadAllAsync"/> has
        /// finished; in the editor the asset is created when the project has none.
        /// </summary>
        public static T Instance => (T)SettingsRegistry.Get(typeof(T));

        /// <summary>The settings if they are available now - never creates the asset, never throws.</summary>
        public static bool TryGet(out T settings)
        {
            var found = SettingsRegistry.TryGet(typeof(T), out var asset);
            settings = found ? (T)asset : null;
            return found;
        }
    }
}
