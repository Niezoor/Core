using System.Threading;
using Core.Utilities;
using UnityEngine;

namespace Core.Bootstrap
{
    /// <summary>
    /// Loads <see cref="BootSettings.StartScene"/> without activating it, so leaving the splash is instant. Added by
    /// <see cref="Boot"/> as the last task, never listed in the settings.
    /// </summary>
    internal sealed class FirstSceneTask : SplashTask
    {
        private readonly SceneReference scene;
        private readonly SceneLoadOperation load;

        public override string DisplayName => scene is { IsSet: true } ? $"Load {scene.Name}" : "Load first scene";

        public FirstSceneTask(SceneReference scene)
        {
            this.scene = scene;
            if (scene is { IsSet: true }) load = new SceneLoadOperation(scene);
            Required = true;
        }

        public override async Awaitable RunAsync(SplashContext context, CancellationToken cancellationToken)
        {
            if (load == null)
            {
                Debug.LogWarning("[Boot] BootSettings has no Entry Scene; the splash will stay on screen.");
                return;
            }

            // A retry after a failed load starts over; a load that is still valid is simply awaited again.
            await load.WaitLoadedAsync(context.ReportProgress, cancellationToken);
        }

        public void Activate()
        {
            if (load != null) _ = load.ActivateAsync(Application.exitCancellationToken);
        }
    }
}
