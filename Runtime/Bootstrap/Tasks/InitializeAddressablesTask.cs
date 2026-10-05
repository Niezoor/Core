using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Core.Bootstrap.Tasks
{
    /// <summary>Starts Addressables and, for remote content, brings the catalog up to date.</summary>
    [Serializable]
    public sealed class InitializeAddressablesTask : SplashTask
    {
        [Tooltip("Ask the content server for a newer catalog. A failure here never stops the game - the cached " +
                 "or built-in catalog is used.")]
        [SerializeField] private bool updateCatalogs;

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

            if (updateCatalogs) await UpdateCatalogsAsync(context, cancellationToken);
        }

        private static async Awaitable UpdateCatalogsAsync(SplashContext context, CancellationToken cancellationToken)
        {
            context.ReportStatus("core.boot.checking_content");
            var check = Addressables.CheckForCatalogUpdates(false);
            try
            {
                await AddressablesAwait.CompletionAsync(check, null, cancellationToken);
                if (check.Status != AsyncOperationStatus.Succeeded || check.Result == null || check.Result.Count == 0)
                {
                    if (check.Status != AsyncOperationStatus.Succeeded)
                        Debug.LogWarning($"[Boot] Catalog check failed, using the cached catalog: {check.OperationException}");
                    return;
                }

                var update = Addressables.UpdateCatalogs(check.Result, false);
                try
                {
                    await AddressablesAwait.CompletionAsync(update, context, cancellationToken);
                    if (update.Status != AsyncOperationStatus.Succeeded)
                        Debug.LogWarning($"[Boot] Catalog update failed, using the cached catalog: {update.OperationException}");
                }
                finally
                {
                    if (update.IsValid()) Addressables.Release(update);
                }
            }
            finally
            {
                if (check.IsValid()) Addressables.Release(check);
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
