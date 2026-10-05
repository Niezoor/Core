using Core.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    /// <summary>The default fields (so <c>[SubclassPicker]</c> draws the step lists) plus the boot scene's state.</summary>
    [CustomEditor(typeof(BootSettings))]
    internal sealed class BootSettingsEditor : UnityEditor.Editor
    {
        // The status reads scene files, so it is refreshed now and then rather than on every repaint.
        private const double RefreshInterval = 2.0;

        private BootSceneSetup.Status status;
        private string bootScenePath;
        private double refreshedAt = double.NegativeInfinity;

        private void OnEnable() => Refresh();

        public override void OnInspectorGUI()
        {
            if (EditorApplication.timeSinceStartup - refreshedAt > RefreshInterval) Refresh();

            DrawBootScene();
            DrawFirstSceneWarning();
            if (GUILayout.Button("Open Boot Monitor")) BootMonitorWindow.Open();
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        private void Refresh()
        {
            status = BootSceneSetup.GetStatus(out bootScenePath);
            refreshedAt = EditorApplication.timeSinceStartup;
        }

        private void DrawBootScene()
        {
            switch (status)
            {
                case BootSceneSetup.Status.Ok:
                    EditorGUILayout.HelpBox($"Boot scene: {bootScenePath}", MessageType.Info);
                    return;
                case BootSceneSetup.Status.NotFirst:
                    EditorGUILayout.HelpBox($"{bootScenePath} has BootScene but is not first in Build Settings.",
                        MessageType.Warning);
                    break;
                case BootSceneSetup.Status.Missing:
                    EditorGUILayout.HelpBox("No scene has a BootScene component. It is set up when you press Play " +
                                            "or build, or now:", MessageType.Warning);
                    break;
                default:
                    return;
            }

            if (!GUILayout.Button("Setup Boot Scene")) return;
            if (!BootSceneSetup.Ensure(true, out var message)) Debug.LogError($"[Boot] {message}");
            Refresh();
            GUIUtility.ExitGUI();
        }

        private void DrawFirstSceneWarning()
        {
            var settings = (BootSettings)target;
            if (!settings.FirstScene)
            {
                EditorGUILayout.HelpBox("No First Scene: the splash will finish loading and stay on screen.",
                    MessageType.Warning);
                return;
            }

            if (bootScenePath != null && settings.FirstScene.ScenePath == bootScenePath)
            {
                EditorGUILayout.HelpBox("First Scene is the boot scene itself - the splash would load itself again.",
                    MessageType.Error);
            }
        }
    }
}
