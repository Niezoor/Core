#if UNITY_6000_3_OR_NEWER
using System;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Core.Editor.TimeTracker
{
    // Unity 6.3 added an extensible main toolbar (UnityEditor.Toolbars.MainToolbar);
    // older editors don't have this API at all, hence the version guard above.
    internal static class TimeTrackerMainToolbar
    {
        private const string ElementPath = "Core/Time Tracker";
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);
        private static DateTime lastRefresh;

        static TimeTrackerMainToolbar()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            var now = DateTime.Now;
            if (now - lastRefresh < RefreshInterval) return;
            lastRefresh = now;
            MainToolbar.Refresh(ElementPath);
        }

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Right)]
        public static MainToolbarElement CreateElement()
        {
            var totalTime = TimeTracker.instance.TotalTime;
            var text = $"{Monospaced($"{(int)totalTime.TotalHours}")}:{Monospaced($"{totalTime.Minutes:00}")}:{Monospaced($"{totalTime.Seconds:00}")}";
            var icon = (Texture2D)(TimeTrackerUpdater.Paused ? EditorIcons.Play.Active : EditorIcons.Pause.Active);
            var content = new MainToolbarContent(text, icon, "Total tracked editor time. Click to pause/resume, right-click to edit.");

            return new MainToolbarButton(content, ToggleTracking)
            {
                populateContextMenu = menu => menu.AppendAction("Edit Time...", action => TimeEditWindow.Open(action.eventInfo?.mousePosition ?? Vector2.zero))
            };
        }

        // Every digit takes the same width, so the ticking clock doesn't resize the toolbar.
        private static string Monospaced(string digits) => $"<mspace=0.6em>{digits}</mspace>";

        private static void ToggleTracking()
        {
            if (TimeTrackerUpdater.Paused) TimeTrackerUpdater.Resume();
            else TimeTrackerUpdater.Pause();
            MainToolbar.Refresh(ElementPath);
        }

        private class TimeEditWindow : EditorWindow
        {
            private const string FieldName = "TimeTrackerEdit";
            private static readonly Vector2 Size = new(180f, 48f);

            private string text;
            private bool focused;

            public static void Open(Vector2 toolbarPosition)
            {
                var time = TimeTracker.instance.TotalTime;
                var window = CreateInstance<TimeEditWindow>();
                window.text = $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}";

                // The menu's position is relative to the toolbar, at the top of the main window.
                var position = EditorGUIUtility.GetMainWindowPosition().position + toolbarPosition;
                window.ShowAsDropDown(new Rect(position, Vector2.zero), Size);
            }

            private void OnGUI()
            {
                // Read before the field, which would otherwise take the key.
                var current = Event.current;
                if (current.type == EventType.KeyDown)
                {
                    if (current.keyCode == KeyCode.Escape)
                    {
                        Close();
                        return;
                    }

                    if (current.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                    {
                        Apply();
                        return;
                    }
                }

                GUI.SetNextControlName(FieldName);
                text = EditorGUILayout.TextField(text);
                if (!focused)
                {
                    focused = true;
                    EditorGUI.FocusTextInControl(FieldName);
                }

                var valid = TimeTracker.TryParseTotalHoursString(text, out _);
                EditorGUILayout.LabelField(valid ? "Enter applies, Esc cancels" : "Format: h:mm:ss", EditorStyles.miniLabel);
            }

            private void Apply()
            {
                if (!TimeTracker.TryParseTotalHoursString(text, out var time)) return;

                Undo.RecordObject(TimeTracker.instance, "Set time on time tracker");
                TimeTracker.instance.TotalTime = time;
                TimeTracker.instance.SaveSettings();
                MainToolbar.Refresh(ElementPath);
                Close();
            }
        }
    }
}
#endif
