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
        public bool debug;

        private float prevValue;
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
            if (bar.Value < reduceFillImage.fillAmount && prevValue > bar.Value)
            {
                if (debug) Debug.Log($"Start reduce effect for {this}", this);
                if (coroutine != null) StopCoroutine(coroutine);
                if (gameObject.activeInHierarchy) coroutine = StartCoroutine(UpdateAnimatedCoroutine());
                //delayTween = DOVirtual.DelayedCall(delay, UpdateAnimated, false).OnKill(() => { delayTween = null; });
            }
            else
            {
                reduceFillImage.fillAmount = bar.Value;
            }

            prevValue = bar.Value;
        }

        private void UpdateAnimated()
        {
            delayTween = null;
            animTween = DOVirtual.Float(reduceFillImage.fillAmount, bar.Value, duration,
                newVal => reduceFillImage.fillAmount = newVal).SetEase(ease).OnKill(() => { animTween = null; });
        }

        private IEnumerator UpdateAnimatedCoroutine()
        {
            if (debug) Debug.Log($"Start coroutine effect for {this}", this);
            yield return delaySeconds;
            var diff = reduceFillImage.fillAmount - bar.Value;
            var speed = diff / duration;
            while (reduceFillImage.fillAmount > bar.Value)
            {
                //if (debug) Debug.Log($"Update coroutine effect for {this}", this);
                reduceFillImage.fillAmount -= speed * TimeCache.deltaTime;
                yield return waitForEndOfFrame;
            }

            if (debug) Debug.Log($"End coroutine effect for {this}", this);
            reduceFillImage.fillAmount = bar.Value;
        }
    }
}