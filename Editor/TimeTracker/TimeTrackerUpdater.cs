using System;
using UnityEditor;
using UnityEngine;

namespace Core.Editor.TimeTracker
{
    [InitializeOnLoad]
    public static class TimeTrackerUpdater
    {
        private static DateTime prevUpdateTime;
        private static DateTime lastSaveTime;
        private static readonly TimeSpan maxDeltaTime = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan saveInterval = TimeSpan.FromMinutes(1);

        public static bool Paused { get; private set; }

        static TimeTrackerUpdater()
        {
            lastSaveTime = DateTime.Now;
            prevUpdateTime = DateTime.Now;
            EditorApplication.update -= UpdateTime;
            EditorApplication.update += UpdateTime;
            EditorApplication.quitting -= SaveTimeData;
            EditorApplication.quitting += SaveTimeData;
            Paused = false;
        }

        public static void Pause()
        {
            Paused = true;
            EditorApplication.update -= UpdateTime;
        }

        public static void Resume()
        {
            prevUpdateTime = DateTime.Now;
            Paused = false;
            EditorApplication.update -= UpdateTime;
            EditorApplication.update += UpdateTime;
        }

        private static void SaveTimeData()
        {
            TimeTracker.instance.SaveSettings();
            lastSaveTime = DateTime.Now;
        }

        private static void UpdateTime()
        {
            var deltaTime = DateTime.Now - prevUpdateTime;
            var timeSinceLastSave = DateTime.Now - lastSaveTime;
            if (deltaTime < maxDeltaTime)
            {
                TimeTracker.instance.TotalTime += deltaTime;
            }

            if (timeSinceLastSave > saveInterval)
            {
                SaveTimeData();
            }

            prevUpdateTime = DateTime.Now;
        }
    }
}