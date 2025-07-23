using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace Core.UI.Settings.Options
{
    public class OptionEnum : Option
    {
        [SerializeField] private LocalizeStringEvent localizedValueText;

        private Array enumValues;
        private object currentValue;

        public override void PrevOption()
        {
            if (PropertyInfo == null || Obj == null || enumValues == null) return;
            var index = Array.IndexOf(enumValues, currentValue);
            index = (index - 1 + enumValues.Length) % enumValues.Length;
            currentValue = enumValues.GetValue(index);
            PropertyInfo.SetValue(Obj, currentValue);
            UpdateLocalizedText(currentValue);
        }

        public override void NextOption()
        {
            if (PropertyInfo == null || Obj == null || enumValues == null) return;
            var index = Array.IndexOf(enumValues, currentValue);
            index = (index + 1) % enumValues.Length;
            currentValue = enumValues.GetValue(index);
            PropertyInfo.SetValue(Obj, currentValue);
            UpdateLocalizedText(currentValue);
        }

        public override void Clicked()
        { }

        protected override void SetupValue(PropertyInfo propertyInfo, object obj)
        {
            enumValues = Enum.GetValues(propertyInfo.PropertyType);
            currentValue = propertyInfo.GetValue(obj);
            UpdateLocalizedText(currentValue);
        }

        private void UpdateLocalizedText(object value)
        {
            var valueName = Enum.GetName(PropertyInfo.PropertyType, value);
            localizedValueText.StringReference = new LocalizedString(OptionTitle.LocalizationTable, valueName);
        }
    }
}