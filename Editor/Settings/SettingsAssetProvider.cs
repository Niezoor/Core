using System;
using System.Linq;
using System.Reflection;
using Core.Utilities.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Core.Editor
{
    /// <summary>A Project Settings page for every <see cref="SettingsAsset"/> type with [SettingsMenu].</summary>
    internal sealed class SettingsAssetProvider : SettingsProvider
    {
        private readonly Type type;
        private SettingsAsset asset;
        private UnityEditor.Editor editor;
        private bool keywordsBuilt;

        private SettingsAssetProvider(Type type, string path) : base($"Project/{path}", SettingsScope.Project)
        {
            this.type = type;
        }

        [SettingsProviderGroup]
        private static SettingsProvider[] CreateProviders() =>
            SettingsAssetDatabase.Types
                .Select(t => (type: t, menu: t.GetCustomAttribute<SettingsMenuAttribute>()))
                .Where(t => t.menu != null)
                .Select(t => (SettingsProvider)new SettingsAssetProvider(t.type, t.menu.Path))
                .ToArray();

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            // Opening the page is the opt-in: the asset is created here when the project has none.
            asset = SettingsAssetDatabase.GetOrCreate(type);
            base.OnActivate(searchContext, rootElement);
        }

        public override void OnDeactivate()
        {
            DestroyEditor();
            base.OnDeactivate();
        }

        public override void OnGUI(string searchContext)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(10);
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(10);
                    DrawContent();
                }
            }
        }

        private void DrawContent()
        {
            if (!asset)
            {
                DestroyEditor();
                EditorGUILayout.HelpBox($"The project has no {type.Name} asset.", MessageType.Info);
                if (GUILayout.Button("Create")) asset = SettingsAssetDatabase.GetOrCreate(type);
                return;
            }

            if (!editor || editor.target != asset)
            {
                DestroyEditor();
                editor = UnityEditor.Editor.CreateEditor(asset);
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(asset, type, false);
            }

            EditorGUILayout.LabelField(SettingsRegistry.IsPreloaded(type)
                ? "Preloaded: in memory from launch, with everything it references."
                : "Async: in a build loaded by SettingsRegistry.LoadAllAsync (the splash's Load Settings task).",
                EditorStyles.miniLabel);
            EditorGUILayout.Space();
            editor.OnInspectorGUI();
        }

        public override bool HasSearchInterest(string searchContext)
        {
            if (!keywordsBuilt)
            {
                // Searching never creates the asset.
                var found = SettingsAssetDatabase.Find(type);
                if (found) keywords = GetSearchKeywordsFromSerializedObject(new SerializedObject(found));
                keywordsBuilt = true;
            }

            return base.HasSearchInterest(searchContext);
        }

        private void DestroyEditor()
        {
            if (editor) Object.DestroyImmediate(editor);
            editor = null;
        }
    }
}
