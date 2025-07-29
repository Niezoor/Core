using System.Linq;
using System.Reflection;
using Core.UI.Settings.Options;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Core.UI.Settings
{
    public class OptionsFactory : MonoBehaviour
    {
        [SerializeField] private Transform optionsParent;
        [SerializeField] private OptionBool boolOptionPrefab;
        [SerializeField] private OptionEnum enumOptionPrefab;
        [SerializeField] private OptionNumber numberOptionPrefab;
        [SerializeField] private OptionString stringOptionPrefab;
        [SerializeField] private OptionButton buttonPrefab;

        public Transform OptionsParent
        {
            get => optionsParent;
            set => optionsParent = value;
        }

        public void CreateOptions(object target)
        {
            var properties = target.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.IsPublic || f.GetCustomAttribute<SerializeField>() != null)
                .OrderBy(f => f.MetadataToken);
            foreach (var propertyInfo in properties)
            {
                if (propertyInfo.Name == nameof(hideFlags)) continue;
                CrateField(propertyInfo, target);
            }

            var methods = target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.GetCustomAttribute<ButtonAttribute>() != null && m.GetParameters().Length == 0);
            foreach (var methodInfo in methods)
            {
                CreateButton(methodInfo, target);
            }
        }

        public void CrateField(FieldInfo fieldInfo, object obj)
        {
            Option option;
            if (fieldInfo.FieldType == typeof(bool))
            {
                option = Instantiate(boolOptionPrefab, optionsParent);
            }
            else if (fieldInfo.FieldType.IsEnum)
            {
                option = Instantiate(enumOptionPrefab, optionsParent);
            }
            else if (fieldInfo.FieldType == typeof(int) || fieldInfo.FieldType == typeof(float))
            {
                option = Instantiate(numberOptionPrefab, optionsParent);
            }
            else if (fieldInfo.FieldType == typeof(string))
            {
                option = Instantiate(stringOptionPrefab, optionsParent);
            }
            else
            {
                Debug.Log($"Option type {fieldInfo.FieldType.Name} is not supported.");
                return;
            }

            if (option) option.Setup(fieldInfo, obj);
        }

        public void CreateButton(MethodInfo methodInfo, object obj)
        {
            Instantiate(buttonPrefab, optionsParent).Setup(methodInfo, obj);
        }
    }
}