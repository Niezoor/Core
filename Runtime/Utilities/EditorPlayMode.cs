#if UNITY_EDITOR
using UnityEditor;

namespace Core.Utilities
{
    [InitializeOnLoad]
    public static class EditorPlayMode
    {
        public static PlayModeStateChange CurrentPlayModeState { get; private set; }

        static EditorPlayMode()
        {
            EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.playModeStateChanged += ModeChanged;
        }

        static void ModeChanged(PlayModeStateChange playModeState)
        {
            CurrentPlayModeState = playModeState;
        }
    }
}
#endif