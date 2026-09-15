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
            var text = $"{(int)totalTime.TotalHours}:{totalTime.Minutes:00}:{totalTime.Seconds:00}";
            var icon = (Texture2D)(TimeTrackerUpdater.Paused ? EditorIcons.Play.Active : EditorIcons.Pause.Active);
            var content = new MainToolbarContent(text, icon, "Total tracked editor time. Click to pause/resume.");

            return new MainToolbarButton(content, ToggleTracking);
        }

        private static void ToggleTracking()
        {
            if (TimeTrackerUpdater.Paused) TimeTrackerUpdater.Resume();
            else TimeTrackerUpdater.Pause();
            MainToolbar.Refresh(ElementPath);
        }
    }
}
#endif
