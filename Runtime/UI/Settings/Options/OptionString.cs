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

        protected override void SetupValue(FieldInfo fieldInfo, object obj)
        {
            textField.SetTextWithoutNotify((string)fieldInfo.GetValue(obj));
            textField.onEndEdit.AddListener((value) => { fieldInfo.SetValue(obj, value); });
            textField.readOnly = fieldInfo.GetCustomAttribute<ReadOnlyAttribute>() != null;
        }
    }
}