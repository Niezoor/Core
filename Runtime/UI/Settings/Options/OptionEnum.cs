using System;
using System.Reflection;
using Core.Utilities.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace Core.UI.Settings.Options
{
    public class OptionEnum : Option
    {
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private LocalizeStringEvent localizedValueText;

        private Array enumValues;
        private object currentValue;

        public override void PrevOption()
        {
            if (FieldInfo == null || Obj == null || enumValues == null) return;
            var index = Array.IndexOf(enumValues, currentValue);
            index = (index - 1 + enumValues.Length) % enumValues.Length;
            currentValue = enumValues.GetValue(index);
            FieldInfo.SetValue(Obj, currentValue);
            UpdateLocalizedText(currentValue);
        }

        public override void NextOption()
        {
            if (FieldInfo == null || Obj == null || enumValues == null) return;
            var index = Array.IndexOf(enumValues, currentValue);
            index = (index + 1) % enumValues.Length;
            currentValue = enumValues.GetValue(index);
            FieldInfo.SetValue(Obj, currentValue);
            UpdateLocalizedText(currentValue);
        }

        public override void Clicked()
        {
            NextOption();
        }

        protected override void SetupValue(FieldInfo fieldInfo, object obj)
        {
            enumValues = Enum.GetValues(fieldInfo.FieldType);
            currentValue = fieldInfo.GetValue(obj);
            UpdateLocalizedText(currentValue);
        }

        private void UpdateLocalizedText(object value)
        {
            var valueName = Enum.GetName(FieldInfo.FieldType, value);
            if (localizedValueText.StringReference != null)
            {
                localizedValueText.enabled = true;
                localizedValueText.StringReference.StringChanged -= CheckLocalizedResult;
            }

            localizedValueText.StringReference = new LocalizedString(OptionTitle.LocalizationTable, valueName);
            localizedValueText.StringReference.StringChanged += CheckLocalizedResult;
        }

        private void CheckLocalizedResult(string value)
        {
            if (value.Contains("No translation found for"))
            {
                localizedValueText.enabled = false;
                valueText.text = StringExtension.Prettify(currentValue.ToString());
            }
        }
    }
}