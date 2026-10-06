using Core.Display;
using UnityEngine;

namespace Core.UISystem.UGUI
{
    /// <summary>
    /// Fits the RectTransform to the safe area (<see cref="ScreenState.SafeInsets"/>) on the chosen edges. Put it on a
    /// full-screen child of a screen-space canvas.
    /// </summary>
    [AddComponentMenu("Core/UI System/Safe Area Fitter")]
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private SafeAreaEdges edges = SafeAreaEdges.All;

        private ScreenState applied;
        private bool hasApplied;

        public SafeAreaEdges Edges
        {
            get => edges;
            set
            {
                edges = value;
                Apply(ScreenProfile.Current);
            }
        }

        private void OnEnable()
        {
            ScreenProfile.Changed += Apply;
            hasApplied = false;
            Apply(ScreenProfile.Current);
        }

        private void OnDisable()
        {
            ScreenProfile.Changed -= Apply;
        }

        private void Update()
        {
            // Outside Play Mode nothing raises Changed; the Game view can still be resized.
            if (!Application.isPlaying) Apply(ScreenProfile.Current);
        }

        private void Apply(ScreenState state)
        {
            if (hasApplied && state.Equals(applied)) return;
            applied = state;
            hasApplied = true;

            var insets = state.SafeInsets;
            var width = (float)state.Width;
            var height = (float)state.Height;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(
                (edges & SafeAreaEdges.Left) != 0 ? insets.Left / width : 0f,
                (edges & SafeAreaEdges.Bottom) != 0 ? insets.Bottom / height : 0f);
            rect.anchorMax = new Vector2(
                (edges & SafeAreaEdges.Right) != 0 ? 1f - insets.Right / width : 1f,
                (edges & SafeAreaEdges.Top) != 0 ? 1f - insets.Top / height : 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void OnValidate()
        {
            hasApplied = false;
        }
    }
}
