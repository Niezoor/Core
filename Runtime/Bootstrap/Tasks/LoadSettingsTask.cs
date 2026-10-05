using System;
using System.Threading;
using Core.Utilities.Settings;
using UnityEngine;

namespace Core.Bootstrap.Tasks
{
    /// <summary>
    /// Loads every <see cref="SettingsAsset"/> that is not [PreloadedSettings] (<see cref="SettingsRegistry.LoadAllAsync"/>).
    /// Without it - or another call to LoadAllAsync - reading those settings in a build throws.
    /// </summary>
    [Serializable]
    public sealed class LoadSettingsTask : SplashTask
    {
        public override string DisplayName => "Load settings";

        public override Awaitable RunAsync(SplashContext context, CancellationToken cancellationToken) =>
            SettingsRegistry.LoadAllAsync(cancellationToken, context.ReportProgress);
    }
}
