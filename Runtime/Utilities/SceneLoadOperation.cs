using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
#endif

namespace Core.Utilities
{
    /// <summary>
    /// A <see cref="SceneReference"/> loading in the background without activating, so the switch itself is
    /// instant. From Build Settings when the scene is listed there, from Addressables otherwise.
    /// </summary>
    public sealed class SceneLoadOperation
    {
        private readonly SceneReference scene;
        private readonly LoadSceneMode mode;
        private AsyncOperationHandle<SceneInstance> addressableLoad;
        private AsyncOperation builtInLoad;

        public SceneReference Scene => scene;

        public SceneLoadOperation(SceneReference scene, LoadSceneMode mode = LoadSceneMode.Single)
        {
            this.scene = scene ?? throw new ArgumentNullException(nameof(scene));
            this.mode = mode;
        }

        /// <summary>
        /// Starts the load (or picks up one already under way) and waits until only the activation is left.
        /// A failed load throws and starts over on the next call.
        /// </summary>
        public async Awaitable WaitLoadedAsync(Action<float> reportProgress, CancellationToken cancellationToken)
        {
            if (!scene.IsSet) throw new InvalidOperationException("No scene to load");

            if (addressableLoad.IsValid() || (builtInLoad == null && UsesAddressables()))
            {
                if (!addressableLoad.IsValid())
                {
                    addressableLoad = Addressables.LoadSceneAsync(scene.AddressableKey, mode,
                        SceneReleaseMode.ReleaseSceneWhenSceneUnloaded, activateOnLoad: false);
                }

                while (!addressableLoad.IsDone)
                {
                    reportProgress?.Invoke(addressableLoad.PercentComplete);
                    await Awaitable.NextFrameAsync(cancellationToken);
                }

                if (addressableLoad.Status != AsyncOperationStatus.Succeeded)
                {
                    var exception = addressableLoad.OperationException ?? new Exception($"Loading {scene} failed");
                    Addressables.Release(addressableLoad);
                    addressableLoad = default;
                    throw exception;
                }

                reportProgress?.Invoke(1f);
                return;
            }

            builtInLoad ??= StartBuiltInLoad() ??
                            throw new Exception($"{scene} is neither in Build Settings nor Addressable");
            builtInLoad.allowSceneActivation = false;

            // Without activation a scene load stops at 0.9.
            while (builtInLoad.progress < 0.9f)
            {
                reportProgress?.Invoke(builtInLoad.progress / 0.9f);
                await Awaitable.NextFrameAsync(cancellationToken);
            }

            reportProgress?.Invoke(1f);
        }

        /// <summary>Activates the loaded scene, returning once it is active.</summary>
        public async Awaitable ActivateAsync(CancellationToken cancellationToken)
        {
            if (addressableLoad.IsValid())
            {
                await addressableLoad.Result.ActivateAsync();
                return;
            }

            if (builtInLoad == null) return;

            builtInLoad.allowSceneActivation = true;
            while (!builtInLoad.isDone) await Awaitable.NextFrameAsync(cancellationToken);
        }

        private bool UsesAddressables()
        {
            if (scene.IsInBuildSettings) return false;
#if UNITY_EDITOR
            // Outside Build Settings and Addressables the editor still loads it, so a scene played directly reloads.
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            return settings && !string.IsNullOrEmpty(scene.Guid) && settings.FindAssetEntry(scene.Guid, true) != null;
#else
            return true;
#endif
        }

        private AsyncOperation StartBuiltInLoad()
        {
#if UNITY_EDITOR
            if (!scene.IsInBuildSettings)
            {
                return EditorSceneManager.LoadSceneAsyncInPlayMode(scene.Path, new LoadSceneParameters(mode));
            }
#endif
            return SceneManager.LoadSceneAsync(scene.Path, mode);
        }
    }
}
