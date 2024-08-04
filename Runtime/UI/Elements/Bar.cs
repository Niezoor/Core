using System;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Elements
{
    public class Bar : MonoBehaviour
    {
        public Action OnChangeValue;

        public SlicedFilledImage fillImage;
        public Image backgroundImage;

        public float Value => fillImage.fillAmount;

        public void SetValue(float value)
        {
            fillImage.fillAmount = Mathf.Clamp01(value);
            OnChangeValue?.Invoke();
        }

        public void SetSize(float size)
        {
            var sizeDelta = backgroundImage.rectTransform.sizeDelta;
            sizeDelta.x = size;
            backgroundImage.rectTransform.sizeDelta = sizeDelta;
        }
    }
}