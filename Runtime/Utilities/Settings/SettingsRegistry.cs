using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Core.Utilities.Settings
{
    /// <summary>
    /// Where <see cref="SettingsAsset{T}.Instance"/> comes from. In a build: preloaded types from memory, the rest
    /// after <see cref="LoadAllAsync"/>. In the editor: the asset itself, found (or created) by the editor; reads that
    /// would fail in a build - during boot initializers or while <see cref="LoadAllAsync"/> is still running - are
    /// logged as errors.
    /// </summary>
    public static class SettingsRegistry
    {
        /// <summary>The Addressables label every async settings asset carries; the editor keeps it there.</summary>
        public const string AddressablesLabel = "Settings";

        /// <summary>The async settings are in memory (or the build has none).</summary>
        public static bool IsLoaded { get; private set; }

        public static bool IsLoading { get; private set; }

        /// <summary>
        /// Set by the editor: finds the asset of a type (and when asked, creates it). Null in a build, and in tests
        /// that check how a build behaves.
        /// </summary>
        internal static Func<Type, bool, SettingsAsset> EditorResolver;

        /// <summary>
        /// Set while code runs that must not need async settings - Boot's initializers, before anything async could
        /// have loaded - so the editor reports such a read instead of quietly resolving it.
        /// </summary>
        internal static bool PreloadedOnly;

        private static readonly Dictionary<Type, SettingsAsset> settings = new();
        private static readonly HashSet<Type> missingPreloaded = new();
        private static readonly HashSet<Type> reported = new();

        // Held for the whole run: releasing it would let Addressables unload the settings.
        private static AsyncOperationHandle<IList<SettingsAsset>> loadHandle;
        private static Exception loadFailure;
        private static float loadProgress;
        private static int session;

        public static bool IsPreloaded(Type type) => type.IsDefined(typeof(PreloadedSettingsAttribute), false);

        /// <summary>
        /// Loads every async settings asset (those under <see cref="AddressablesLabel"/>) and keeps it for the rest of
        /// the run. Can be called again and from several places at once; after a failure the next call tries again.
        /// </summary>
        /// <param name="progress">0..1 while loading.</param>
        public static async Awaitable LoadAllAsync(CancellationToken cancellationToken = default,
            Action<float> progress = null)
        {
            if (IsLoaded) return;
            if (!IsLoading) RunLoad();

            while (IsLoading)
            {
                progress?.Invoke(loadProgress);
                await Awaitable.NextFrameAsync(cancellationToken);
            }

            if (IsLoaded) return;
            if (loadFailure != null) ExceptionDispatchInfo.Capture(loadFailure).Throw();
            throw new OperationCanceledException("Loading settings stopped with the play session.");
        }

        internal static SettingsAsset Get(Type type)
        {
            if (TryGetLoaded(type, out var asset)) return asset;

            if (EditorResolver != null)
            {
                if (Application.isPlaying) ReportEditorOnlyRead(type);
                asset = EditorResolver(type, true);
                if (asset) settings[type] = asset;
                return asset;
            }

            throw new InvalidOperationException(MissingMessage(type));
        }

        internal static bool TryGet(Type type, out SettingsAsset asset)
        {
            if (TryGetLoaded(type, out asset)) return true;

            asset = EditorResolver?.Invoke(type, false);
            if (!asset)
            {
                asset = null;
                return false;
            }

            settings[type] = asset;
            return true;
        }

        private static bool TryGetLoaded(Type type, out SettingsAsset asset)
        {
            // A destroyed asset (deleted in the editor) compares to null and is looked up again.
            if (settings.TryGetValue(type, out asset) && asset) return true;

            asset = EditorResolver == null && IsPreloaded(type) ? FindPreloaded(type) : null;
            if (!asset)
            {
                asset = null;
                return false;
            }

            settings[type] = asset;
            return true;
        }

        // Preloaded Assets are in memory from launch; searched once per type.
        private static SettingsAsset FindPreloaded(Type type)
        {
            if (missingPreloaded.Contains(type)) return null;

            SettingsAsset found = null;
            var count = 0;
            foreach (var candidate in Resources.FindObjectsOfTypeAll(type))
            {
                if (candidate.GetType() != type) continue;
                found ??= (SettingsAsset)candidate;
                count++;
            }

            if (count > 1) Debug.LogWarning($"[Settings] {count} {type.Name} assets are loaded; using {found.name}.");
            if (!found) missingPreloaded.Add(type);
            return found;
        }

        private static void ReportEditorOnlyRead(Type type)
        {
            if (IsPreloaded(type) || reported.Contains(type)) return;

            string message;
            if (PreloadedOnly)
            {
                message = $"{type.Name} was read during boot initializers, before anything async could load. A build " +
                          "throws here - mark it [PreloadedSettings] or read it later.";
            }
            else if (IsLoading)
            {
                message = $"{type.Name} was read before SettingsRegistry.LoadAllAsync finished. A build throws here - " +
                          "wait for the load, or mark it [PreloadedSettings].";
            }
            else if (IsLoaded)
            {
                message = $"{type.Name} was not loaded by SettingsRegistry.LoadAllAsync - its asset is not under the " +
                          $"Addressables label \"{AddressablesLabel}\", so a build throws here. Run Core > Settings > " +
                          "Sync Settings Assets.";
            }
            else
            {
                // Nothing loads settings in this session, e.g. a gameplay scene played directly: resolving is expected.
                return;
            }

            reported.Add(type);
            Debug.LogError($"[Settings] {message}");
        }

        private static string MissingMessage(Type type)
        {
            if (IsPreloaded(type))
            {
                return $"{type.Name} is [PreloadedSettings] but the build has no asset of it in Preloaded Assets. " +
                       "Open the project in the editor (Core > Settings > Sync Settings Assets) and build again.";
            }

            if (IsLoaded)
            {
                return $"{type.Name} was not loaded by SettingsRegistry.LoadAllAsync: the build has no asset of it " +
                       $"under the Addressables label \"{AddressablesLabel}\". Open the project in the editor " +
                       "(Core > Settings > Sync Settings Assets) and build again.";
            }

            return $"{type.Name} loads asynchronously and is not loaded yet: await SettingsRegistry.LoadAllAsync() " +
                   "first (the splash's LoadSettingsTask does), or mark it [PreloadedSettings].";
        }

        private static async void RunLoad()
        {
            var started = session;
            IsLoading = true;
            loadFailure = null;
            loadProgress = 0f;
            try
            {
                var assets = await LoadAssetsAsync();
                if (started != session) return;

                var seen = new HashSet<Type>();
                foreach (var asset in assets)
                {
                    if (!asset) continue;
                    var type = asset.GetType();
                    if (IsPreloaded(type))
                    {
                        Debug.LogWarning($"[Settings] {type.Name} is [PreloadedSettings] but also under the " +
                                         $"Addressables label \"{AddressablesLabel}\" - the build carries two copies; " +
                                         "the preloaded one is used.");
                        continue;
                    }

                    if (!seen.Add(type))
                    {
                        Debug.LogError($"[Settings] More than one {type.Name} is under the Addressables label " +
                                       $"\"{AddressablesLabel}\"; using the first.");
                        continue;
                    }

                    // In the editor this may replace the asset resolved before - a build would use the loaded one.
                    settings[type] = asset;
                }

                IsLoaded = true;
            }
            catch (Exception exception)
            {
                if (started == session) loadFailure = exception;
            }
            finally
            {
                if (started == session) IsLoading = false;
            }
        }

        private static async Awaitable<IList<SettingsAsset>> LoadAssetsAsync()
        {
            // Polled frame by frame, never through handle.Task, which WebGL can't block on.
            var stop = Application.exitCancellationToken;

            // Loading a label nothing carries fails outright, so an empty one is found out first.
            var locationsHandle = Addressables.LoadResourceLocationsAsync(AddressablesLabel, typeof(SettingsAsset));
            List<IResourceLocation> locations;
            try
            {
                while (!locationsHandle.IsDone) await Awaitable.NextFrameAsync(stop);
                if (locationsHandle.Status != AsyncOperationStatus.Succeeded)
                    throw new Exception("Finding the settings in Addressables failed", locationsHandle.OperationException);
                locations = new List<IResourceLocation>(locationsHandle.Result);
            }
            finally
            {
                if (locationsHandle.IsValid()) Addressables.Release(locationsHandle);
            }

            if (locations.Count == 0) return Array.Empty<SettingsAsset>();

            var handle = Addressables.LoadAssetsAsync<SettingsAsset>(locations, null);
            try
            {
                while (!handle.IsDone)
                {
                    loadProgress = handle.PercentComplete;
                    await Awaitable.NextFrameAsync(stop);
                }

                if (handle.Status != AsyncOperationStatus.Succeeded)
                    throw new Exception("Loading the settings from Addressables failed", handle.OperationException);
            }
            catch
            {
                Addressables.Release(handle);
                throw;
            }

            loadHandle = handle;
            return handle.Result;
        }

        internal static void ResetForTests() => ResetStatics();

        // Domain reload is off in Enter Play Mode settings, so nothing static may survive into the next session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            session++;
            settings.Clear();
            missingPreloaded.Clear();
            reported.Clear();
            // Addressables drops its own state with the session; the handle is only forgotten.
            loadHandle = default;
            loadFailure = null;
            loadProgress = 0f;
            IsLoaded = false;
            IsLoading = false;
            PreloadedOnly = false;
        }
    }
}
