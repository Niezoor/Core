using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Core.Bootstrap.Tasks
{
    /// <summary>
    /// Loads every asset with a label and keeps it loaded for the rest of the run, so the game finds it in memory -
    /// by default the "Settings" label of the old <c>ScriptableObjectSettings</c>. <c>SettingsAsset</c> types need
    /// <see cref="LoadSettingsTask"/> instead: only it hands them to <c>SettingsRegistry</c>.
    /// </summary>
    [Serializable]
    public sealed class PreloadAddressablesLabelTask : SplashTask
    {
        [SerializeField] private string label = "Settings";

        // Held for the whole run: releasing would let Addressables unload what the game expects to stay.
        private static readonly HashSet<string> LoadedLabels = new();

        public override string DisplayName => $"Preload \"{label}\"";

        public override async Awaitable RunAsync(SplashContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(label) || LoadedLabels.Contains(label)) return;

            // Loading a label nothing carries fails outright; an empty label is just nothing to do.
            var locations = Addressables.LoadResourceLocationsAsync(label);
            int count;
            try
            {
                await AddressablesAwait.CompletionAsync(locations, null, cancellationToken);
                AddressablesAwait.ThrowIfFailed(locations, $"Finding assets labelled \"{label}\" failed");
                count = locations.Result.Count;
            }
            finally
            {
                if (locations.IsValid()) Addressables.Release(locations);
            }

            if (count > 0)
            {
                var load = Addressables.LoadAssetsAsync<UnityEngine.Object>(label, null);
                try
                {
                    await AddressablesAwait.CompletionAsync(load, context, cancellationToken);
                    AddressablesAwait.ThrowIfFailed(load, $"Preloading \"{label}\" failed");
                }
                catch
                {
                    Addressables.Release(load);
                    throw;
                }
            }

            LoadedLabels.Add(label);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => LoadedLabels.Clear();
    }
}
