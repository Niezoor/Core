using System.Reflection;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Core.UI.Settings.Options
{
    [RequireComponent(typeof(OptionTitle))]
    public class OptionString : Option
    {
        [SerializeField] private TMP_InputField textField;

        public override void PrevOption()
        { }

        public override void NextOption()
        { }

        public override void Clicked()
        { }

        protected override void SetupValue(PropertyInfo propertyInfo, object obj)
        {
            textField.SetTextWithoutNotify((string)propertyInfo.GetValue(obj));
            textField.onEndEdit.AddListener((value) => { propertyInfo.SetValue(obj, value); });
            textField.readOnly = propertyInfo.GetCustomAttribute<ReadOnlyAttribute>() != null;
        }
    }
}