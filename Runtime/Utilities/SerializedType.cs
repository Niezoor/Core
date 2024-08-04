using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Core.Utilities
{
    [Serializable]
    public class SerializedType<T> : ISerializationCallbackReceiver
    {
        [SerializeField] private string typeName;

#if UNITY_EDITOR
        // HACK: I wasn't able to find the base type from the SerializedProperty,
        // so I'm smuggling it in via an extra string stored only in-editor.
        [SerializeField] string baseTypeName;
#endif

        public Type Value;

        public SerializedType(Type typeToStore)
        {
            Value = typeToStore;
        }

        public void OnBeforeSerialize()
        {
            if (Value != null)
            {
                typeName = Value.AssemblyQualifiedName;
            }

#if UNITY_EDITOR
            baseTypeName = typeof(T).AssemblyQualifiedName;
#endif
        }

        public void OnAfterDeserialize()
        {
            if (string.IsNullOrEmpty(typeName) || typeName == "null")
            {
                Value = null;
                return;
            }

            Value = Type.GetType(typeName);
        }

        public static implicit operator Type(SerializedType<T> t) => t.Value;

        // TODO: Validate that t is a subtype of T?
        public static implicit operator SerializedType<T>(Type t) => new SerializedType<T>(t);
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(SerializedType<>), true)]
    public class InspectableTypeDrawer : PropertyDrawer
    {
        Type[] _derivedTypes;
        GUIContent[] _optionLabels;
        int _selectedIndex;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var storedProperty = property.FindPropertyRelative("typeName");
            string qualifiedName = storedProperty.stringValue;

            if (_optionLabels == null)
            {
                Initialize(property, storedProperty);
            }
            else if (_selectedIndex == _derivedTypes.Length)
            {
                if (qualifiedName != "null") UpdateIndex(storedProperty);
            }
            else
            {
                if (qualifiedName != _derivedTypes[_selectedIndex].AssemblyQualifiedName) UpdateIndex(storedProperty);
            }

            var propLabel = EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();

            _selectedIndex = EditorGUI.Popup(position, propLabel, _selectedIndex, _optionLabels);

            if (EditorGUI.EndChangeCheck())
            {
                storedProperty.stringValue = _selectedIndex < _derivedTypes.Length
                    ? _derivedTypes[_selectedIndex].AssemblyQualifiedName
                    : "null";
            }

            EditorGUI.EndProperty();
        }

        static Type[] FindAllDerivedTypes(Type baseType)
        {
            return baseType.Assembly
                .GetTypes()
                .Where(t =>
                    t != baseType &&
                    baseType.IsAssignableFrom(t)
                ).ToArray<Type>();
        }

        void Initialize(SerializedProperty property, SerializedProperty stored)
        {
            var baseTypeProperty = property.FindPropertyRelative("baseTypeName");
            var baseType = Type.GetType(baseTypeProperty.stringValue);

            _derivedTypes = FindAllDerivedTypes(baseType);

            if (_derivedTypes.Length == 0)
            {
                _optionLabels = new[] { new GUIContent($"No types derived from {baseType.Name} found.") };
                return;
            }

            _optionLabels = new GUIContent[_derivedTypes.Length + 1];
            for (int i = 0; i < _derivedTypes.Length; i++)
            {
                _optionLabels[i] = new GUIContent(_derivedTypes[i].Name);
            }

            _optionLabels[_derivedTypes.Length] = new GUIContent("null");

            UpdateIndex(stored);
        }

        void UpdateIndex(SerializedProperty stored)
        {
            string qualifiedName = stored.stringValue;

            for (int i = 0; i < _derivedTypes.Length; i++)
            {
                if (_derivedTypes[i].AssemblyQualifiedName == qualifiedName)
                {
                    _selectedIndex = i;
                    return;
                }
            }

            _selectedIndex = _derivedTypes.Length;
            stored.stringValue = "null";
        }
    }
#endif
}