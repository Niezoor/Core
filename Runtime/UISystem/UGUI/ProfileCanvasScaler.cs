using Core.Display;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UISystem.UGUI
{
    /// <summary>
    /// Scales a canvas like the UI Toolkit screens: by <see cref="ScreenState.Scale"/> (density × the player's UI
    /// scale), so a HUD laid out in dp matches the menus on every screen.
    /// </summary>
    [AddComponentMenu("Core/UI System/Profile Canvas Scaler")]
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class ProfileCanvasScaler : MonoBehaviour
    {
        [Tooltip("Extra factor for this canvas only.")]
        [SerializeField, Min(0.1f)] private float multiplier = 1f;

        private CanvasScaler scaler;

        private void OnEnable()
        {
            scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            ScreenProfile.Changed += Apply;
            Apply(ScreenProfile.Current);
        }

        private void OnDisable()
        {
            ScreenProfile.Changed -= Apply;
        }

        private void Apply(ScreenState state)
        {
            scaler.scaleFactor = state.Scale * multiplier;
        }
    }
}
