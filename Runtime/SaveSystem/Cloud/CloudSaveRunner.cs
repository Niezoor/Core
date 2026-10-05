using UnityEngine;

namespace Core.SaveSystem.Cloud
{
    /// <summary>Unity callbacks for <see cref="CloudSave"/>. Created by it alone, never added by hand.</summary>
    [AddComponentMenu("")]
    internal sealed class CloudSaveRunner : MonoBehaviour
    {
        internal static CloudSaveRunner Create()
        {
            var host = new GameObject("[CloudSave]");
            DontDestroyOnLoad(host);
            return host.AddComponent<CloudSaveRunner>();
        }

        private void Update()
        {
            CloudSave.Tick();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) CloudSave.OnApplicationSuspending();
        }
    }
}
