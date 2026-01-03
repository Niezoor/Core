using System;
using System.Collections.Generic;
using System.Globalization;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using FilePathAttribute = UnityEditor.FilePathAttribute;
using FontStyle = UnityEngine.FontStyle;

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
            //var time = totalTime;
            //var inText = time.Days > 0 ? totalTime.ToString(@"d\D\a\y\s\ hh\:mm\:ss") : totalTime.ToString(@"hh\:mm\:ss");
            var inText = $"{(int)totalTime.TotalHours}:{totalTime.Minutes:0#}:{totalTime.Seconds:0#}";
            EditorGUILayout.LabelField("Total time", totalTimeStyle);
            totalTimeStyle.fontSize = 24;
            totalTimeStyle.fontStyle = FontStyle.Bold;
            totalTimeStyle.margin = new RectOffset(0, 0, 0, 0);
            totalTimeStyle.padding = new RectOffset(-30, 0, 0, 0);
            EditorGUILayout.BeginHorizontal();
            var buttonStyle = new GUIStyle(EditorStyles.iconButton);
            buttonStyle.fixedWidth = 30;
            buttonStyle.fixedHeight = 30;
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            buttonStyle.margin = new RectOffset(10, 0, 0, 0);
            var icon = TimeTrackerUpdater.Paused ? EditorIcons.Play.Active : EditorIcons.Pause.Active;
            if (GUILayout.Button(icon, buttonStyle))
            {
                if (TimeTrackerUpdater.Paused) TimeTrackerUpdater.Resume();
                else TimeTrackerUpdater.Pause();
            }

            var outText = EditorGUILayout.TextField(inText, totalTimeStyle, GUILayout.Height(30));
            if (outText != inText)
            {
                try
                {
                    //outText = outText.Replace("Days", ".");
                    //outText = outText.Replace(" ", "");
                    if (TryParseTotalHoursString(outText, out totalTime))
                    {
                        Undo.RecordObject(instance, "Set time on time tracker");
                        instance.TotalTime = totalTime;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private static UnityEditor.Editor editor;
        private static SettingsProvider settingsProvider;

        public void SaveSettings()
        {
            Save(true);
        }

        public static bool TryParseTotalHoursString(string value, out TimeSpan timeSpan)
        {
            timeSpan = TimeSpan.Zero;

            if (string.IsNullOrEmpty(value))
                return false;

            string[] parts = value.Split(':');
            if (parts.Length != 3)
                return false;

            if (!int.TryParse(parts[0], out int hours))
                return false;

            if (!int.TryParse(parts[1], out int minutes))
                return false;

            if (!int.TryParse(parts[2], out int seconds))
                return false;

            if (minutes < 0 || minutes > 59 || seconds < 0 || seconds > 59)
                return false;

            timeSpan = new TimeSpan(hours, minutes, seconds);
            return true;
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
}