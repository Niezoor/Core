using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Core.UISystem.Editor
{
    [CustomPropertyDrawer(typeof(ScreenTypeAttribute))]
    internal sealed class ScreenTypeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "[ScreenType] needs a string field");
                return;
            }

            var types = ScreenRegistry.ScreenTypes;
            var values = types.Select(ScreenTypeAttribute.Serialize).ToList();
            // A '/' in a popup item makes a submenu, so namespaces group the screens.
            var options = types.Select(t => new GUIContent(t.FullName?.Replace('.', '/'))).Prepend(new GUIContent("None")).ToList();

            var current = property.stringValue;
            var index = string.IsNullOrEmpty(current) ? 0 : values.IndexOf(current) + 1;
            if (index == 0 && !string.IsNullOrEmpty(current))
            {
                options.Add(new GUIContent($"Missing: {current}"));
                index = options.Count - 1;
            }

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var selected = EditorGUI.Popup(position, label, index, options.ToArray());
                if (selected == index) return;
                if (selected == 0) property.stringValue = string.Empty;
                else if (selected <= values.Count) property.stringValue = values[selected - 1];
            }
        }
    }
}
