using Core.Bootstrap;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Core.UI
{
    /// <summary>
    /// Shows <see cref="Boot.SplashProgress"/> in the boot scene. Hidden at first and shown only when loading takes longer
    /// than <see cref="showDelay"/>, so a fast start never flashes a bar. Every target is optional.
    /// </summary>
    [AddComponentMenu("Core/UI/Boot Progress View")]
    public sealed class BootProgressView : MonoBehaviour
    {
        [Tooltip("Shown while loading, hidden otherwise. Leave empty to keep the targets always visible.")]
        [SerializeField] private CanvasGroup content;
        [SerializeField, Min(0f)] private float showDelay = 0.5f;
        [SerializeField, Min(0f)] private float fadeDuration = 0.2f;
        [SerializeField] private bool hideWhenReady = true;

        [Header("Targets")]
        [SerializeField] private Image fillImage;
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_Text percentText;
        [SerializeField] private string percentFormat = "{0:0}%";
        [SerializeField] private UnityEvent<float> onProgress;

        [Tooltip("How fast the shown value catches up, in progress per second; 0 = instantly.")]
        [SerializeField, Min(0f)] private float smoothing = 2f;

        private float shown = -1f;
        private float loadingSince;

        private void OnEnable()
        {
            loadingSince = Time.unscaledTime;
            shown = -1f;
            if (content) content.alpha = 0f;
            Apply(Boot.SplashProgress);
        }

        private void Update()
        {
            var target = Boot.SplashProgress;
            var value = smoothing <= 0f || shown < 0f
                ? target
                : Mathf.MoveTowards(shown, target, smoothing * Time.unscaledDeltaTime);
            if (!Mathf.Approximately(value, shown)) Apply(value);

            if (!content) return;

            var state = Boot.SplashState;
            if (state != SplashState.Loading) loadingSince = Time.unscaledTime;
            var visible = state == SplashState.Loading
                ? Time.unscaledTime - loadingSince >= showDelay
                : state == SplashState.Ready && !hideWhenReady;
            var targetAlpha = visible ? 1f : 0f;
            content.alpha = fadeDuration <= 0f
                ? targetAlpha
                : Mathf.MoveTowards(content.alpha, targetAlpha, Time.unscaledDeltaTime / fadeDuration);
        }

        private void Apply(float value)
        {
            shown = value;
            if (fillImage) fillImage.fillAmount = value;
            if (slider) slider.normalizedValue = value;
            if (percentText) percentText.text = string.Format(percentFormat, value * 100f);
            onProgress?.Invoke(value);
        }
    }
}
