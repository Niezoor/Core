using System;
using System.Threading;
using Core.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Core.Bootstrap
{
    /// <summary>
    /// Loads <see cref="BootSettings.FirstScene"/> without activating it, so leaving the splash is instant. Added by
    /// <see cref="Boot"/> as the last task, never listed in the settings.
    /// </summary>
    internal sealed class FirstSceneTask : SplashTask
    {
        private readonly SceneRef scene;
        private AsyncOperationHandle<SceneInstance> addressableLoad;
        private AsyncOperation builtInLoad;

        public override string DisplayName => scene ? $"Load {scene.name}" : "Load first scene";

        public FirstSceneTask(SceneRef scene)
        {
            this.scene = scene;
            Required = true;
        }

        public override async Awaitable RunAsync(SplashContext context, CancellationToken cancellationToken)
        {
            if (!scene || string.IsNullOrEmpty(scene.ScenePath))
            {
                Debug.LogWarning("[Boot] BootSettings has no FirstScene; the splash will stay on screen.");
                return;
            }

            // A retry after a failed load starts over; a load that is still valid is simply awaited again.
            if (scene.IsAddressable)
            {
                if (!addressableLoad.IsValid())
                {
                    addressableLoad = Addressables.LoadSceneAsync(scene.ScenePath, LoadSceneMode.Single,
                        SceneReleaseMode.ReleaseSceneWhenSceneUnloaded, activateOnLoad: false);
                }

                while (!addressableLoad.IsDone)
                {
                    context.ReportProgress(addressableLoad.PercentComplete);
                    await Awaitable.NextFrameAsync(cancellationToken);
                }

                if (addressableLoad.Status != AsyncOperationStatus.Succeeded)
                {
                    var exception = addressableLoad.OperationException ??
                                    new Exception($"Loading {scene.ScenePath} failed");
                    Addressables.Release(addressableLoad);
                    addressableLoad = default;
                    throw exception;
                }

                return;
            }

            if (builtInLoad == null)
            {
                builtInLoad = SceneManager.LoadSceneAsync(scene.ScenePath, LoadSceneMode.Single)
                              ?? throw new Exception($"{scene.ScenePath} is neither Addressable nor in Build Settings");
                builtInLoad.allowSceneActivation = false;
            }

            // Without activation a scene load stops at 0.9.
            while (builtInLoad.progress < 0.9f)
            {
                context.ReportProgress(builtInLoad.progress / 0.9f);
                await Awaitable.NextFrameAsync(cancellationToken);
            }
        }

        public void Activate()
        {
            if (addressableLoad.IsValid())
            {
                addressableLoad.Result.ActivateAsync();
            }
            else if (builtInLoad != null)
            {
                builtInLoad.allowSceneActivation = true;
            }
        }
    }
}
