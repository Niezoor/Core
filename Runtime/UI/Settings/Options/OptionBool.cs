using System.Reflection;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace Core.UI.Settings.Options
{
    public class OptionBool : Option
    {
        private static readonly int isOnBoolHash = Animator.StringToHash("IsOn");

        [SerializeField] private LocalizeStringEvent localizedValue;
        /*[InfoBox("Use animator \"IsOn\" parameter")]
        [SerializeField] private Animator animator;*/

        private bool currentValue = false;

        public override void PrevOption()
        {
            Toggle();
        }

        public override void NextOption()
        {
            Toggle();
        }

        public override void Clicked()
        {
            Toggle();
        }

        public void Toggle()
        {
            currentValue = !currentValue;
            PropertyInfo.SetValue(Obj, currentValue);
            UpdateView();
        }

        protected override void SetupValue(PropertyInfo propertyInfo, object obj)
        {
            currentValue = (bool)propertyInfo.GetValue(obj);
            UpdateView();
        }

        private void UpdateView()
        {
            localizedValue.StringReference = currentValue
                ? new LocalizedString(OptionTitle.LocalizationTable, "On")
                : new LocalizedString(OptionTitle.LocalizationTable, "Off");
            //animator.SetBool(isOnBoolHash, currentValue);
        }
    }
}