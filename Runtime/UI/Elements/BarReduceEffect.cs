using System.Collections;
using Core.Utilities.Optimization;
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
        private Tween animTween;
        private Coroutine coroutine;
        private WaitForSeconds delaySeconds;
        private readonly WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();

        private void Reset()
        {
            bar = GetComponent<Bar>();
        }

        private void Start()
        {
            delaySeconds = new WaitForSeconds(delay);
            bar.OnChangeValue += UpdateValue;
            UpdateValue();
        }

        private void OnDestroy()
        {
            bar.OnChangeValue -= UpdateValue;
            animTween?.Kill();
            delayTween?.Kill();
        }

        private void UpdateValue()
        {
            if (bar.Value < reduceFillImage.fillAmount)
            {
                if (coroutine != null) StopCoroutine(coroutine);
                coroutine = StartCoroutine(UpdateAnimatedCoroutine());
                //delayTween = DOVirtual.DelayedCall(delay, UpdateAnimated, false).OnKill(() => { delayTween = null; });
            }
            else
            {
                reduceFillImage.fillAmount = bar.Value;
            }
        }

        private void UpdateAnimated()
        {
            delayTween = null;
            animTween = DOVirtual.Float(reduceFillImage.fillAmount, bar.Value, duration,
                newVal => reduceFillImage.fillAmount = newVal).SetEase(ease).OnKill(() => { animTween = null; });
        }

        private IEnumerator UpdateAnimatedCoroutine()
        {
            yield return delaySeconds;
            var diff = reduceFillImage.fillAmount - bar.Value;
            var speed = diff / duration;
            while (reduceFillImage.fillAmount > bar.Value)
            {
                reduceFillImage.fillAmount -= speed * TimeCache.deltaTime;
                yield return waitForEndOfFrame;
            }

            reduceFillImage.fillAmount = bar.Value;
        }
    }
}