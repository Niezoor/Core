using System.Linq;
using Core.Bootstrap;
using Core.Bootstrap.Tasks;
using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    /// <summary>The default fields (so <c>[SubclassPicker]</c> draws the step lists) plus the boot scene's state. Opening
    /// it syncs the <c>[BootStep]</c> steps in.</summary>
    [CustomEditor(typeof(BootSettings))]
    internal sealed class BootSettingsEditor : UnityEditor.Editor
    {
        // The status reads scene files, so it is refreshed now and then rather than on every repaint.
        private const double RefreshInterval = 2.0;

        private BootSceneSetup.Status status;
        private string bootScenePath;
        private bool hasAsyncSettings;
        private double refreshedAt = double.NegativeInfinity;

        private void OnEnable()
        {
            // Inspecting the asset shows it complete - also right after it was created, before any compile synced it.
            if (!EditorApplication.isPlayingOrWillChangePlaymode) BootStepSync.Sync((BootSettings)target);
            Refresh();
        }

        public override void OnInspectorGUI()
        {
            if (EditorApplication.timeSinceStartup - refreshedAt > RefreshInterval) Refresh();

            DrawBootScene();
            DrawFirstSceneWarning();
            DrawLoadSettingsWarning();
            if (GUILayout.Button("Open Boot Monitor")) BootMonitorWindow.Open();
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        private void Refresh()
        {
            status = BootSceneSetup.GetStatus(out bootScenePath);
            hasAsyncSettings = SettingsAssetSync.HasAsyncAssets();
            refreshedAt = EditorApplication.timeSinceStartup;
        }

        private void DrawLoadSettingsWarning()
        {
            var settings = (BootSettings)target;
            if (!hasAsyncSettings || settings.SplashTasks.Any(t => t is LoadSettingsTask { Enabled: true })) return;

            EditorGUILayout.HelpBox("The project has settings that load asynchronously, but the Load Settings splash " +
                                    "task is switched off: in a build reading them throws until " +
                                    "SettingsRegistry.LoadAllAsync runs.", MessageType.Warning);
            if (GUILayout.Button("Enable Load Settings Task"))
            {
                Undo.RecordObject(settings, "Enable Load Settings Task");
                foreach (var task in settings.SplashTasks.OfType<LoadSettingsTask>()) task.Enabled = true;
                EditorUtility.SetDirty(settings);
                BootStepSync.Sync(settings);
            }
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
            var scene = settings.StartScene;
            if (!scene.IsSet)
            {
                EditorGUILayout.HelpBox("No Entry Scene: the splash will finish loading and stay on screen.",
                    MessageType.Warning);
                return;
            }

            if (bootScenePath != null && scene.Path == bootScenePath)
            {
                EditorGUILayout.HelpBox("Entry Scene is the boot scene itself - the splash would load itself again.",
                    MessageType.Error);
            }
        }
    }
}
