using UnityEngine;
using UnityEngine.UIElements;

namespace Core.UISystem
{
    /// <summary>
    /// Every frame: input mode, back button and the debug menu shortcut - from launch, before any screen exists.
    /// Created by the UI system alone, never added by hand.
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(-9000)]
    internal sealed class UISystemRunner : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            var host = new GameObject("[UIInput]");
            DontDestroyOnLoad(host);
            host.AddComponent<UISystemRunner>();
            UIInput.IsTyping = IsTyping;
        }

        private void Update()
        {
            UIInput.Update();
            DebugMenu.Poll();
            if (!UIInput.BackPressed) return;
            if (DebugMenu.IsOpen) DebugMenu.Back();
            else Screens.Back();
        }

        private static bool IsTyping() =>
            DebugMenu.IsTyping ||
            Screens.HasHost && Panels.IsTextInput(Screens.Panel?.focusController.focusedElement as VisualElement);
    }
}
