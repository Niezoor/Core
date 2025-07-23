using System.Reflection;
using Core.UI.Settings.Options;
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

        public Transform OptionsParent
        {
            get => optionsParent;
            set => optionsParent = value;
        }

        public void DisplayOptions(object settings)
        {
            var properties = settings.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var propertyInfo in properties)
            {
                if (propertyInfo.Name == nameof(hideFlags)) continue;
                CreateFromProperty(propertyInfo, settings);
            }
        }

        public void CreateFromProperty(PropertyInfo propertyInfo, object obj)
        {
            Option option;
            if (propertyInfo.PropertyType == typeof(bool))
            {
                option = Instantiate(boolOptionPrefab, optionsParent);
            }
            else if (propertyInfo.PropertyType.IsEnum)
            {
                option = Instantiate(enumOptionPrefab, optionsParent);
            }
            else if (propertyInfo.PropertyType == typeof(int) || propertyInfo.PropertyType == typeof(float))
            {
                option = Instantiate(numberOptionPrefab, optionsParent);
            }
            else if (propertyInfo.PropertyType == typeof(string))
            {
                option = Instantiate(stringOptionPrefab, optionsParent);
            }
            else
            {
                Debug.Log($"Option type {propertyInfo.PropertyType.Name} is not supported.");
                return;
            }

            if (option) option.Setup(propertyInfo, obj);
        }
    }
}