using Core.Display;
using UnityEngine;

namespace Core.UISystem.UGUI
{
    /// <summary>
    /// Turns objects on only for some screens and input modes, e.g. on-screen controls for touch, a compact HUD for
    /// phones in portrait. It switches other objects, never its own, so it keeps listening while they are off.
    /// </summary>
    [AddComponentMenu("Core/UI System/Profile Visibility")]
    public sealed class ProfileVisibility : MonoBehaviour
    {
        [SerializeField] private GameObject[] targets = new GameObject[0];
        [SerializeField] private ScreenCondition screen = new();
        [SerializeField] private InputModeMask inputModes = InputModeMask.All;

        public bool IsShown => screen.Matches(ScreenProfile.Current) && UIInput.Matches(inputModes);

        private void OnEnable()
        {
            ScreenProfile.ClassChanged += OnClassChanged;
            UIInput.ModeChanged += OnInputModeChanged;
            Apply();
        }

        private void OnDisable()
        {
            ScreenProfile.ClassChanged -= OnClassChanged;
            UIInput.ModeChanged -= OnInputModeChanged;
        }

        private void OnClassChanged(ScreenState _) => Apply();

        private void OnInputModeChanged(InputMode _) => Apply();

        private void Apply()
        {
            var shown = IsShown;
            foreach (var target in targets)
            {
                if (target && target != gameObject && target.activeSelf != shown) target.SetActive(shown);
            }
        }
    }
}
