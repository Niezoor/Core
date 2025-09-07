using System;
using UnityEditor;
using UnityEngine;

namespace Core.Editor.TimeTracker
{
    [InitializeOnLoad]
    public static class TimeTrackerUpdater
    {
        private static DateTime prevUpdateTime;
        private static readonly TimeSpan maxDeltaTime = TimeSpan.FromMinutes(1);
        
        public static bool Paused { get; private set; }

        static TimeTrackerUpdater()
        {
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
        }

        private static void UpdateTime()
        {
            var deltaTime = DateTime.Now - prevUpdateTime;
            if (deltaTime < maxDeltaTime)
            {
                TimeTracker.instance.TotalTime += deltaTime;
            }
            else
            {
                Debug.LogWarning($"Time Tracker delta time:{deltaTime} was too big, max is {maxDeltaTime}");
            }

            prevUpdateTime = DateTime.Now;
        }
    }
}