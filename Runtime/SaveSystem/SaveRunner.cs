using Core.Utilities.Optimization;
using UnityEngine;

namespace Core.SaveSystem
{
    /// <summary>Unity callbacks for <see cref="Save"/>. Created by it alone, never added by hand.</summary>
    [AddComponentMenu("")]
    internal sealed class SaveRunner : MonoBehaviour
    {
        internal static SaveRunner Create()
        {
            var host = new GameObject("[SaveSystem]");
            DontDestroyOnLoad(host);
            return host.AddComponent<SaveRunner>();
        }

        private void Update()
        {
            Save.Tick(TimeCache.unscaledDeltaTime);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Save.Flush(true);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Save.Flush(true);
        }
    }
}
