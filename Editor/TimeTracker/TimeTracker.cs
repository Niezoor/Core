using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using FilePathAttribute = UnityEditor.FilePathAttribute;

namespace Core.Editor.TimeTracker
{
    [FilePath("ProjectSettings/TimeTracker.asset", FilePathAttribute.Location.ProjectFolder)]
    [HideMonoScript]
    public class TimeTracker : ScriptableSingleton<TimeTracker>
    {
        [SerializeField] [HideInInspector] public long SavedTimeTicks;

        public TimeSpan TotalTime
        {
            get => TimeSpan.FromTicks(SavedTimeTicks);
            set
            {
                SavedTimeTicks = value.Ticks;
                settingsProvider?.Repaint();
            }
        }

        [OnInspectorGUI] private void ShowTextInternal() => ShowTotalTimeGUI();

        [Button]
        public void ResetTime()
        {
            if (EditorUtility.DisplayDialog("Reset Time", "Time will be reset.", "OK", "Cancel"))
            {
                SavedTimeTicks = 0;
                SaveSettings();
            }
        }

        public static void ShowTotalTimeGUI()
        {
            GUIStyle totalTimeStyle = new GUIStyle(EditorStyles.label);
            totalTimeStyle.normal.background = null;
            totalTimeStyle.focused.background = null;
            totalTimeStyle.alignment = TextAnchor.MiddleCenter;
            var totalTime = instance.TotalTime;
            var time = totalTime;
            var inText = time.Days > 0
                ? totalTime.ToString(@"d\D\a\y\s\ hh\:mm\:ss")
                : totalTime.ToString(@"hh\:mm\:ss");
            EditorGUILayout.LabelField("Total time", totalTimeStyle);
            totalTimeStyle.fontSize = 24;
            totalTimeStyle.fontStyle = FontStyle.Bold;
            totalTimeStyle.margin = new RectOffset(0, 0, 0, 0);
            totalTimeStyle.padding = new RectOffset(0, 0, 0, 10);
            var outText = EditorGUILayout.TextField(inText, totalTimeStyle, GUILayout.Height(30));
            if (outText != inText)
            {
                try
                {
                    outText = outText.Replace("Days", ".");
                    outText = outText.Replace(" ", "");
                    Undo.RecordObject(instance, "Set time on time tracker");
                    instance.TotalTime = TimeSpan.Parse(outText);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        private static UnityEditor.Editor editor;
        private static SettingsProvider settingsProvider;

        public void SaveSettings()
        {
            Save(true);
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            settingsProvider = new SettingsProvider("Project/Core/Time Tracker", SettingsScope.Project)
            {
                label = "Time Tracker",
                guiHandler = (searchContext) =>
                {
                    EditorGUI.BeginChangeCheck();
                    if (!editor && instance)
                    {
                        editor = UnityEditor.Editor.CreateEditor(instance);
                    }

                    editor.OnInspectorGUI();
                    if (EditorGUI.EndChangeCheck())
                    {
                        instance.SaveSettings();
                    }
                },

                keywords = new HashSet<string>(new[] { "My", "Tool", "Settings" })
            };

            return settingsProvider;
        }
    }

    public class TimeTrackerWindow : EditorWindow
    {
        [MenuItem("Core/Show Time Tracker")]
        public static void ShowWindow()
        {
            var window = EditorWindow.GetWindow(typeof(TimeTrackerWindow));
            window.titleContent = new GUIContent("Time Tracker");
        }

        private void OnEnable()
        {
            EditorApplication.update -= Repaint;
            EditorApplication.update += Repaint;
        }

        private void OnDestroy()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            TimeTracker.ShowTotalTimeGUI();
        }
    }
}