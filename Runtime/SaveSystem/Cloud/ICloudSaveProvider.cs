using System.Threading;
using UnityEngine;

namespace Core.SaveSystem.Cloud
{
    /// <summary>
    /// One cloud service holding one save. Knows nothing about conflicts - <see cref="CloudSync"/> decides what to
    /// move where. Failures are thrown; every call must complete on the main thread.
    /// </summary>
    public interface ICloudSaveProvider
    {
        string Name { get; }

        /// <summary>Signed in and reachable as far as the provider knows, without asking the service.</summary>
        bool IsAvailable { get; }

        /// <returns>Whether the provider is usable now.</returns>
        Awaitable<bool> SignInAsync(CancellationToken cancellationToken);

        /// <returns>The stored <see cref="SaveSnapshot.Text"/>, or null when the cloud holds no save.</returns>
        Awaitable<string> DownloadAsync(CancellationToken cancellationToken);

        Awaitable UploadAsync(SaveSnapshot snapshot, CancellationToken cancellationToken);
    }
}
