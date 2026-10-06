using UnityEngine;

namespace Core.Display
{
    /// <summary>Refreshes <see cref="ScreenProfile"/> every frame. Created by it alone, never added by hand.</summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(-10000)] // Before UI scripts, so they read this frame's screen.
    internal sealed class ScreenProfileRunner : MonoBehaviour
    {
        internal static void Create()
        {
            var host = new GameObject("[ScreenProfile]");
            DontDestroyOnLoad(host);
            host.AddComponent<ScreenProfileRunner>();
        }

        private void Update()
        {
            ScreenProfile.Refresh();
        }
    }
}
