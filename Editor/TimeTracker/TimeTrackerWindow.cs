using UnityEditor;
using UnityEngine;

namespace Core.Editor.TimeTracker
{
    public class TimeTrackerWindow : EditorWindow
    {
        [MenuItem("Window/Time Tracker")]
        public static void ShowWindow()
        {
            var window = GetWindow<TimeTrackerWindow>();
            window.titleContent = new GUIContent("Time Tracker");
            window.minSize = new Vector2(200, 60);
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