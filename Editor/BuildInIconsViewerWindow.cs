#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    public class BuiltInIconsViewerWindow : EditorWindow
    {
        private Vector2 scrollPosition = Vector2.zero;
        private Dictionary<string, GUIContent> iconContents = new Dictionary<string, GUIContent>();

        private static readonly string[] generalIcons = { "_Popup", "_Help", "Clipboard", "editicon.sml", "Mirror", "SpeedScale", "Icon Dropdown", "Avatar Icon", "AvatarPivot", "SettingsIcon", "VerticalSplit", "HorizontalSplit", "WaitSpin00", "UnityLogo", "AgeiaLogo", "MonoLogo", "AboutWindow.MainHeader", "Search Icon", "Filter Icon", "TreeEditor.Refresh", "TreeEditor.Duplicate", "TreeEditor.Trash", "EyeDropper.Large", "PreTextureRGB", "d_DebuggerEnabled", "d_DebuggerDisabled", "console.erroricon", "console.warnicon", "console.infoicon", "animationvisibilitytoggleoff", "animationvisibilitytoggleon", "Animation.AddKeyframe", "Animation.NextKey", "Animation.PrevKey", "Animation.AddEvent", "Animation.Record", "Animation.Play", "AnimatorController Icon", "PlayButtonProfile On", "StepButton On", "PauseButton On", "PlayButton On", "PlayButtonProfile", "StepButton", "PauseButton", "PlayButton", "AnimationClip Icon", "Toolbar Minus", "Toolbar Plus More", "Toolbar Plus", "d_Toolbar Plus", "d_Toolbar Minus" };
        private static readonly string[] fileTypeIcons = { "TextAsset Icon", "Shader Icon", "boo Script Icon", "cs Script Icon", "js Script Icon", "Prefab Icon", "PrefabModel Icon", "GameObject Icon", "MeshRenderer Icon", "Terrain Icon", "Camera Icon", "Light Icon", "ScriptableObject Icon", "Folder Icon", "Material Icon", "Texture Icon", "AudioClip Icon" };
        private static readonly string[] sceneViewToolIcons = { "ViewToolOrbit On", "ViewToolZoom On", "ViewToolMove On", "ViewToolOrbit", "ViewToolZoom", "ViewToolMove", "ScaleTool On", "RotateTool On", "MoveTool On", "ScaleTool", "RotateTool", "MoveTool", "d_ViewToolOrbit", "d_ViewToolMove", "d_ViewToolZoom", "d_ToolHandleGlobal", "d_ToolHandleLocal", "SceneviewAudio", "SceneviewLighting", "d_scenevis_visible", "d_scenevis_hidden", "d_scenevis_visible_hover" };
        private static readonly string[] svGizmoIcons = { "sv_icon_none", "sv_icon_dot15_pix16_gizmo", "sv_icon_dot1_sml", "sv_icon_dot4_sml", "sv_icon_dot7_sml", "sv_icon_dot5_pix16_gizmo", "sv_icon_dot11_pix16_gizmo", "sv_icon_dot12_sml", "sv_icon_dot15_sml", "sv_icon_dot9_pix16_gizmo", "sv_icon_name6", "sv_icon_name3", "sv_icon_name4", "sv_icon_name0", "sv_icon_name1", "sv_icon_name2", "sv_icon_name5", "sv_icon_name7", "sv_icon_dot1_pix16_gizmo", "sv_icon_dot8_pix16_gizmo", "sv_icon_dot2_pix16_gizmo", "sv_icon_dot6_pix16_gizmo", "sv_icon_dot0_sml", "sv_icon_dot3_sml", "sv_icon_dot6_sml", "sv_icon_dot9_sml", "sv_icon_dot11_sml", "sv_icon_dot14_sml", "sv_label_0", "sv_label_1", "sv_label_2", "sv_label_3", "sv_label_5", "sv_label_6", "sv_label_7", "sv_icon_dot14_pix16_gizmo", "sv_icon_dot7_pix16_gizmo", "sv_icon_dot3_pix16_gizmo", "sv_icon_dot0_pix16_gizmo", "sv_icon_dot2_sml", "sv_icon_dot5_sml", "sv_icon_dot8_sml", "sv_icon_dot10_pix16_gizmo", "sv_icon_dot12_pix16_gizmo", "sv_icon_dot10_sml", "sv_icon_dot13_sml", "sv_icon_dot4_pix16_gizmo", "sv_label_4", "sv_icon_dot13_pix16_gizmo" };
        private static readonly string[] buildSettingsIcons = { "BuildSettings.SelectedIcon", "BuildSettings.Web.Small", "BuildSettings.Standalone.Small", "BuildSettings.iPhone.Small", "BuildSettings.Android.Small", "BuildSettings.XBox360.Small", "BuildSettings.XboxOne.Small", "BuildSettings.PSP2.Small", "BuildSettings.PS4.Small", "BuildSettings.PSM.Small", "BuildSettings.FlashPlayer.Small", "BuildSettings.Metro.Small", "BuildSettings.WP8.Small", "BuildSettings.Web", "BuildSettings.Standalone", "BuildSettings.iPhone", "BuildSettings.Android", "BuildSettings.XBox360", "BuildSettings.XboxOne", "BuildSettings.PSP2", "BuildSettings.PS4", "BuildSettings.PSM", "BuildSettings.FlashPlayer", "BuildSettings.Metro", "BuildSettings.WP8" };

        private static readonly (string Label, string[] Icons)[] categorizedIcons = {
            ("General", generalIcons),
            ("File Types", fileTypeIcons),
            ("Scene View Tools", sceneViewToolIcons),
            ("Gizmos", svGizmoIcons),
            ("Build Settings", buildSettingsIcons)
        };

        [MenuItem("Window/Built-in Icons Viewer")]
        public static void ShowWindow() => GetWindow<BuiltInIconsViewerWindow>("Built-in Icons");

        void OnEnable()
        {
            iconContents.Clear();
            List<string> missingIconNames = new List<string>();
            HashSet<string> processedIcons = new HashSet<string>();

            foreach (var category in categorizedIcons)
            {
                foreach (string iconName in category.Icons)
                {
                    if (processedIcons.Contains(iconName)) continue;
                    processedIcons.Add(iconName);

                    GUIContent iconContent = EditorGUIUtility.IconContent(iconName);
                    if (iconContent != null && iconContent.image != null)
                    {
                        GUIContent displayContent = new GUIContent(iconContent.image);
                        displayContent.tooltip = iconName;
                        iconContents[iconName] = displayContent;
                    }
                    else
                        missingIconNames.Add(iconName);
                }
            }
            if (missingIconNames.Count > 0)
                Debug.LogWarning($"Failed to load the following built-in icons: {string.Join(", ", missingIconNames)}");
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Click an icon to copy its name to the clipboard:", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            float iconSize = 30f;
            float totalCellWidth = iconSize + 2;
            float scrollbarWidth = 15f;
            foreach (var category in categorizedIcons)
            {
                var availableIconsInCategory = category.Icons.Where(iconName => iconContents.ContainsKey(iconName)).ToList();
                if (availableIconsInCategory.Count == 0) continue;
                EditorGUILayout.LabelField(category.Label, EditorStyles.boldLabel);
                GUILayout.BeginVertical();
                int currentIconIndex = 0;
                while (currentIconIndex < availableIconsInCategory.Count)
                {
                    float availableWidth = position.width - scrollbarWidth;
                    int iconsPerRow = Mathf.Max(1, Mathf.FloorToInt(availableWidth / totalCellWidth));

                    EditorGUILayout.BeginHorizontal();
                    for (int j = 0; j < iconsPerRow && currentIconIndex < availableIconsInCategory.Count; j++)
                    {
                        string iconName = availableIconsInCategory[currentIconIndex];
                        if (GUILayout.Button(iconContents[iconName], GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
                        {
                            EditorGUIUtility.systemCopyBuffer = iconContents[iconName].tooltip;
                            Debug.Log($"Copied to clipboard: {iconContents[iconName].tooltip}");
                        }
                        currentIconIndex++;
                    }
                    GUILayout.FlexibleSpace(); 
                    EditorGUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
                GUILayout.Space(10);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}

#endif