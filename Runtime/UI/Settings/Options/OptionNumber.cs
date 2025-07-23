using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Settings.Options
{
    public class OptionNumber : Option
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private TMP_InputField textField;
        [SerializeField] private Slider slider;
        [SerializeField] private float floatStep = 0.1f;

        private bool isFloat;
        private bool isClamped;
        private Vector2 minMax;
        private float currentValue;
        private float prevValue;

        public override void PrevOption()
        {
            if (isFloat)
            {
                currentValue -= floatStep;
            }
            else
            {
                currentValue -= 1;
            }

            PropertyInfo.SetValue(Obj, currentValue);
            UpdateView();
        }

        public override void NextOption()
        {
            if (isFloat)
            {
                currentValue += floatStep;
            }
            else
            {
                currentValue += 1;
            }

            PropertyInfo.SetValue(Obj, currentValue);
            UpdateView();
        }

        public override void Clicked()
        {
            if (currentValue != 0)
            {
                prevValue = currentValue;
                currentValue = 0;
            }
            else
            {
                currentValue = prevValue;
            }

            PropertyInfo.SetValue(Obj, currentValue);
            UpdateView();
        }

        protected override void SetupValue(PropertyInfo propertyInfo, object obj)
        {
            if (propertyInfo.PropertyType == typeof(float))
            {
                isFloat = true;
                slider.wholeNumbers = false;
                textField.contentType = TMP_InputField.ContentType.DecimalNumber;
                prevValue = currentValue = (float)propertyInfo.GetValue(obj);
                textField.onEndEdit.AddListener(ParseValueFloat);
            }

            if (propertyInfo.PropertyType == typeof(int))
            {
                isFloat = false;
                slider.wholeNumbers = true;
                textField.contentType = TMP_InputField.ContentType.IntegerNumber;
                prevValue = currentValue = (int)propertyInfo.GetValue(obj);
                textField.onEndEdit.AddListener(ParseValueInt);
            }

            UpdateView();
            var rangeAttribute = propertyInfo.GetCustomAttribute<RangeAttribute>();
            if (rangeAttribute != null) SetMinMax(rangeAttribute.min, rangeAttribute.max);
            else SetUnclamped();
        }

        private void UpdateView()
        {
            currentValue = (float)PropertyInfo.GetValue(Obj);
            var valueString =
                isFloat ? currentValue.ToString(CultureInfo.InvariantCulture) : ((int)currentValue).ToString();
            text.text = valueString;
            textField.SetTextWithoutNotify(valueString);
        }

        private void ParseValueInt(string value)
        {
            if (int.TryParse(value, out var result))
            {
                if (isClamped) result = Mathf.Clamp(result, Mathf.RoundToInt(minMax.x), Mathf.RoundToInt(minMax.y));
                PropertyInfo.SetValue(Obj, result);
            }
        }

        private void ParseValueFloat(string value)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                if (isClamped) result = Mathf.Clamp(result, minMax.x, minMax.y);
                PropertyInfo.SetValue(Obj, result);
            }
        }

        private void SetMinMax(float min, float max)
        {
            isClamped = true;
            slider.gameObject.SetActive(true);
            text.enabled = true;
            textField.enabled = false;
            slider.minValue = min;
            slider.maxValue = max;
            minMax = new Vector2(min, max);
        }

        private void SetUnclamped()
        {
            isClamped = false;
            slider.gameObject.SetActive(false);
            text.enabled = false;
            textField.enabled = true;
        }
    }
}