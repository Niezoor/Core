using Core.InputSystemExtension.Manager;
using Core.Utilities.Settings;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Core.InputSystemExtension
{
    public class InputSystemSettings : ScriptableObjectSettings<InputSystemSettings>
    {
        public InputActionAsset InputActionAsset;

#if UNITY_EDITOR
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider() => GetDefaultSettings("Game/Input");

        private Editor managerEditor;

        [OnInspectorGUI]
        private void ShowManagerInspector()
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("BOX");
            if (InputManager.HasInstance)
            {
                if (!managerEditor || !managerEditor.serializedObject.targetObject)
                {
                    managerEditor = Editor.CreateEditor(InputManager.Instance);
                }

                GUI.enabled = false;
                EditorGUILayout.ObjectField(InputManager.Instance, typeof(InputManager), true);
                GUI.enabled = true;
                managerEditor.OnInspectorGUI();
            }
            else
            {
                EditorGUILayout.LabelField("Enter Play Mode to display Manager editor");
            }

            EditorGUILayout.EndVertical();
        }
#endif
    }
}