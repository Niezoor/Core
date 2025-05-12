using DG.Tweening;
using UnityEngine;

namespace Core.UI.Elements
{
    [RequireComponent(typeof(Bar))]
    public class BarReduceEffect : MonoBehaviour
    {
        public Bar bar;
        public SlicedFilledImage reduceFillImage;
        public float delay = 0.66f;
        public float duration = 0.33f;
        public Ease ease = Ease.Linear;

        private Tween delayTween;

        private void Reset()
        {
            bar = GetComponent<Bar>();
        }

        private void Start()
        {
            bar.OnChangeValue += UpdateValue;
            UpdateValue();
        }

        private void OnDestroy()
        {
            bar.OnChangeValue -= UpdateValue;
        }

        private void UpdateValue()
        {
            if (bar.Value < reduceFillImage.fillAmount)
            {
                if (delayTween != null) return;
                delayTween = DOVirtual.DelayedCall(delay, UpdateAnimated, false).OnKill(() => { delayTween = null; });
            }
            else
            {
                reduceFillImage.fillAmount = bar.Value;
            }
        }

        private void UpdateAnimated()
        {
            delayTween = null;
            DOVirtual.Float(reduceFillImage.fillAmount, bar.Value, duration,
                newVal => reduceFillImage.fillAmount = newVal).SetEase(ease);
        }
    }
}