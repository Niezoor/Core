using System;
using UnityEditor;

namespace Core.Editor.TimeTracker
{
    [InitializeOnLoad]
    public static class TimeTrackerUpdater
    {
        private static DateTime prevUpdateTime;

        static TimeTrackerUpdater()
        {
            prevUpdateTime = DateTime.Now;
            EditorApplication.update -= UpdateTime;
            EditorApplication.update += UpdateTime;
            EditorApplication.quitting -= SaveTimeData;
            EditorApplication.quitting += SaveTimeData;
        }

        private static void SaveTimeData()
        {
            TimeTracker.instance.SaveSettings();
        }

        private static void UpdateTime()
        {
            var deltaTime = DateTime.Now - prevUpdateTime;
            TimeTracker.instance.TotalTime += deltaTime;
            prevUpdateTime = DateTime.Now;
        }
    }
}