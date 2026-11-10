using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Core.Bootstrap.Tasks
{
    /// <summary>
    /// Starts Addressables, with progress. Whether it asks the content server for a newer catalog is the project's
    /// Addressables setting "Disable Catalog Update on Startup" - a failed check falls back to the cached catalog.
    /// </summary>
    [Serializable, BootStep(Order = BootOrder.Addressables)]
    public sealed class InitializeAddressablesTask : SplashTask
    {
        public override string DisplayName => "Initialize Addressables";

        public override async Awaitable RunAsync(SplashContext context, CancellationToken cancellationToken)
        {
            var init = Addressables.InitializeAsync(false);
            try
            {
                await AddressablesAwait.CompletionAsync(init, context, cancellationToken);
                AddressablesAwait.ThrowIfFailed(init, "Addressables failed to initialize");
            }
            finally
            {
                if (init.IsValid()) Addressables.Release(init);
            }
        }
    }

    internal static class AddressablesAwait
    {
        /// <summary>Waits frame by frame - never through handle.Task, which WebGL can't block on.</summary>
        public static async Awaitable CompletionAsync(AsyncOperationHandle handle, SplashContext context,
            CancellationToken cancellationToken)
        {
            while (!handle.IsDone)
            {
                context?.ReportProgress(handle.PercentComplete);
                await Awaitable.NextFrameAsync(cancellationToken);
            }
        }

        public static void ThrowIfFailed(AsyncOperationHandle handle, string message)
        {
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new Exception(message, handle.OperationException);
        }
    }
}
