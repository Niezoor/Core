using Core.Utilities;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Core.Editor
{
    /// <summary>A scene field, warning when the scene could not be loaded in a build.</summary>
    [CustomPropertyDrawer(typeof(SceneReference))]
    public sealed class SceneReferenceDrawer : PropertyDrawer
    {
        private const float WarningHeight = 30f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = EditorGUIUtility.singleLineHeight;
            if (!IsLoadable(property)) height += WarningHeight + EditorGUIUtility.standardVerticalSpacing;
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var assetProperty = property.FindPropertyRelative("sceneAsset");
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            var asset = EditorGUI.ObjectField(line, label, assetProperty.objectReferenceValue, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck()) assetProperty.objectReferenceValue = asset;
            EditorGUI.EndProperty();

            if (IsLoadable(property)) return;

            var warning = new Rect(position.x, line.yMax + EditorGUIUtility.standardVerticalSpacing, position.width,
                WarningHeight);
            EditorGUI.HelpBox(warning, "Neither in Build Settings nor Addressable - it loads only in the editor.",
                MessageType.Warning);
        }

        private static bool IsLoadable(SerializedProperty property)
        {
            var asset = property.FindPropertyRelative("sceneAsset").objectReferenceValue;
            if (!asset) return true;

            var path = AssetDatabase.GetAssetPath(asset);
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && scene.path == path) return true;
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            return settings && settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path), true) != null;
        }
    }
}
