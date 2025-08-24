#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.Utilities.Settings
{
    public class ScriptableObjectSettingsProvider<T> : SettingsProvider where T : ScriptableObjectSettings<T>
    {
        private readonly ScriptableObject settings;
        private SerializedObject serializedSettings;
        private bool keywordsBuilt;
        private UnityEditor.Editor editor;

        private SerializedObject SerializedSettings => serializedSettings ??= new SerializedObject(settings);

        public ScriptableObjectSettingsProvider(T instance, string settingsName) : base($"Project/{settingsName}",
            SettingsScope.Project)
        {
            settings = instance;
        }

        public override void OnActivate(string searchContext,
            VisualElement rootElement)
        {
            editor = UnityEditor.Editor.CreateEditor(settings);
            base.OnActivate(searchContext, rootElement);
        }

        public override void OnDeactivate()
        {
            Object.DestroyImmediate(editor);
            editor = null;
            base.OnDeactivate();
        }

        public override void OnGUI(string searchContext)
        {
            if (settings == null || editor == null) return;
            EditorGUIUtility.labelWidth = 250;
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUILayout.BeginVertical();
            GUILayout.Space(10);
            GUI.enabled = false;
            EditorGUILayout.ObjectField(settings, settings.GetType(), false);
            GUI.enabled = true;
            //GUILayout.BeginVertical("HelpBox");
            editor.OnInspectorGUI();
            //GUILayout.EndVertical();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            EditorGUIUtility.labelWidth = 0;
        }

        public override bool HasSearchInterest(string searchContext)
        {
            if (!keywordsBuilt)
            {
                keywords = GetSearchKeywordsFromSerializedObject(SerializedSettings);
                keywordsBuilt = true;
            }

            return base.HasSearchInterest(searchContext);
        }
    }
}
#endif