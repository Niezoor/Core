using System;
using System.Collections.Generic;
using System.Threading;
using Core.Utilities.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Core.Bootstrap.Tasks
{
    /// <summary>
    /// Loads every asset with a label and keeps it loaded for the rest of the run, so the game finds it in memory -
    /// here the "Settings" label of the old <c>ScriptableObjectSettings</c>; nothing to do in a project without them.
    /// <c>SettingsAsset</c> types under the label are left to <see cref="LoadSettingsTask"/>, the only one that hands
    /// them to <c>SettingsRegistry</c>. For another label, subclass it with its own <see cref="BootStepAttribute"/>.
    /// </summary>
    [Serializable, BootStep(Order = BootOrder.Settings)]
    public class PreloadAddressablesLabelTask : SplashTask
    {
        // Held for the whole run: releasing would let Addressables unload what the game expects to stay.
        private static readonly HashSet<string> LoadedLabels = new();

        protected virtual string Label => SettingsRegistry.AddressablesLabel;

        public override string DisplayName => $"Preload \"{Label}\"";

        public override async Awaitable RunAsync(SplashContext context, CancellationToken cancellationToken)
        {
            var label = Label;
            if (string.IsNullOrEmpty(label) || LoadedLabels.Contains(label)) return;

            // Loading a label nothing carries fails outright; an empty label is just nothing to do.
            var handle = Addressables.LoadResourceLocationsAsync(label);
            try
            {
                await AddressablesAwait.CompletionAsync(handle, null, cancellationToken);
                AddressablesAwait.ThrowIfFailed(handle, $"Finding assets labelled \"{label}\" failed");

                var locations = new List<IResourceLocation>();
                foreach (var location in handle.Result)
                {
                    if (!typeof(SettingsAsset).IsAssignableFrom(location.ResourceType)) locations.Add(location);
                }

                if (locations.Count > 0)
                {
                    var load = Addressables.LoadAssetsAsync<UnityEngine.Object>(locations, null);
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
            }
            finally
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }

            LoadedLabels.Add(label);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => LoadedLabels.Clear();
    }
}
