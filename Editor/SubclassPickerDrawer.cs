using System;
using System.Collections.Generic;
using System.Linq;
using Core.Utilities;
using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    [CustomPropertyDrawer(typeof(SubclassPickerAttribute))]
    public sealed class SubclassPickerDrawer : PropertyDrawer
    {
        private static readonly Dictionary<Type, Type[]> CandidatesCache = new();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
                return EditorGUIUtility.singleLineHeight;
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.LabelField(position, label.text, "[SubclassPicker] needs [SerializeReference]");
                return;
            }

            UnshareDuplicatedReference(property);

            var header = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var buttonRect = new Rect(header.x + EditorGUIUtility.labelWidth + 2f, header.y,
                header.width - EditorGUIUtility.labelWidth - 2f, header.height);

            var value = property.managedReferenceValue;
            var typeName = value == null ? "None" : ObjectNames.NicifyVariableName(value.GetType().Name);
            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(typeName), FocusType.Keyboard))
            {
                ShowMenu(property);
            }

            // The default drawing adds the foldout and the child fields; the dropdown sits where a value would.
            EditorGUI.PropertyField(position, property, label, true);
        }

        private void ShowMenu(SerializedProperty property)
        {
            var baseType = GetBaseType(property);
            var menu = new GenericMenu();
            var current = property.managedReferenceValue?.GetType();
            var path = property.propertyPath;
            var serializedObject = property.serializedObject;

            menu.AddItem(new GUIContent("None"), current == null, () => Assign(serializedObject, path, null));
            foreach (var type in GetCandidates(baseType))
            {
                var label = ObjectNames.NicifyVariableName(type.Name);
                if (type.Namespace != null) label = $"{type.Namespace.Replace('.', '/')}/{label}";
                menu.AddItem(new GUIContent(label), type == current,
                    () => Assign(serializedObject, path, Activator.CreateInstance(type)));
            }

            menu.ShowAsContext();
        }

        private static void Assign(SerializedObject serializedObject, string path, object value)
        {
            serializedObject.Update();
            var property = serializedObject.FindProperty(path);
            property.managedReferenceValue = value;
            property.isExpanded = value != null;
            serializedObject.ApplyModifiedProperties();
        }

        private Type GetBaseType(SerializedProperty property)
        {
            var type = fieldInfo.FieldType;
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return type.GetGenericArguments()[0];
            return type;
        }

        private static Type[] GetCandidates(Type baseType)
        {
            if (CandidatesCache.TryGetValue(baseType, out var cached)) return cached;
            cached = TypeCache.GetTypesDerivedFrom(baseType)
                .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition && !typeof(UnityEngine.Object).IsAssignableFrom(t))
                .Where(t => t.IsDefined(typeof(SerializableAttribute), false))
                .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Namespace).ThenBy(t => t.Name)
                .ToArray();
            CandidatesCache[baseType] = cached;
            return cached;
        }

        /// <summary>
        /// The list's "+" copies the last element's reference, so two entries would share one object and editing
        /// either changes both. The later one gets its own copy.
        /// </summary>
        private static void UnshareDuplicatedReference(SerializedProperty property)
        {
            var value = property.managedReferenceValue;
            if (value == null) return;

            var path = property.propertyPath;
            var arrayIndex = path.LastIndexOf(".Array.data[", StringComparison.Ordinal);
            if (arrayIndex < 0) return;

            var array = property.serializedObject.FindProperty(path.Substring(0, arrayIndex));
            var ownIndex = int.Parse(path.Substring(arrayIndex + ".Array.data[".Length).TrimEnd(']'));
            for (var i = 0; i < ownIndex; i++)
            {
                if (array.GetArrayElementAtIndex(i).managedReferenceId != property.managedReferenceId) continue;

                var copy = JsonUtility.FromJson(JsonUtility.ToJson(value), value.GetType());
                property.managedReferenceValue = copy;
                property.serializedObject.ApplyModifiedProperties();
                return;
            }
        }
    }
}
