using System.Linq;
using Core.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    /// <summary>The boot's own fields, the boot scene's state and, read only, the steps the features declared. Opening
    /// it syncs the <c>[BootStep]</c> steps in.</summary>
    [CustomEditor(typeof(BootSettings))]
    internal sealed class BootSettingsEditor : UnityEditor.Editor
    {
        // The status reads scene files, so it is refreshed now and then rather than on every repaint.
        private const double RefreshInterval = 2.0;

        private static bool showSteps;

        private BootSceneSetup.Status status;
        private string bootScenePath;
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
            if (GUILayout.Button("Open Boot Monitor")) BootMonitorWindow.Open();
            EditorGUILayout.Space();
            DrawDefaultInspector();
            EditorGUILayout.Space();
            DrawSteps();
        }

        private void Refresh()
        {
            status = BootSceneSetup.GetStatus(out bootScenePath);
            refreshedAt = EditorApplication.timeSinceStartup;
        }

        /// <summary>Read only: what will run, as the features declared it with [BootStep].</summary>
        private void DrawSteps()
        {
            var settings = (BootSettings)target;
            var count = settings.Initializers.Count + settings.Services.Count + settings.SplashTasks.Count;
            showSteps = EditorGUILayout.Foldout(showSteps, $"Steps from [BootStep] ({count})", true);
            if (!showSteps) return;

            var steps = BootStepSync.Steps;
            using (new EditorGUI.IndentLevelScope())
            {
                DrawPhase("Initializers", settings.Initializers, steps);
                DrawPhase("Services", settings.Services, steps);
                DrawPhase("Splash tasks", settings.SplashTasks, steps);
            }
        }

        private static void DrawPhase<T>(string title, System.Collections.Generic.List<T> entries,
            System.Collections.Generic.IReadOnlyDictionary<System.Type, BootStepAttribute> steps) where T : BootEntry
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            if (entries.Count == 0) EditorGUILayout.LabelField("-", EditorStyles.miniLabel);
            foreach (var entry in entries.Where(e => e != null))
            {
                var order = steps.TryGetValue(entry.GetType(), out var attribute) ? attribute.Order : 0;
                var details = entry.Platforms == BootPlatforms.All ? $"{order}" : $"{order}, {entry.Platforms}";
                if (entry is SplashTask task && !task.Required) details += ", optional";
                EditorGUILayout.LabelField(entry.DisplayName, $"{details}  ({entry.GetType().Assembly.GetName().Name})",
                    EditorStyles.miniLabel);
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
                    EditorGUILayout.HelpBox("No scene has a BootScene component. Play still works, but a build " +
                                            "fails without a boot scene.", MessageType.Warning);
                    break;
                default:
                    return;
            }

            var missing = status == BootSceneSetup.Status.Missing;
            if (!GUILayout.Button(missing ? "Add Boot Scene..." : "Move to Top of Build Settings")) return;
            var done = missing ? BootSceneSetup.AddBootScene(out var message) : BootSceneSetup.FixOrder(out message);
            if (done) Debug.Log($"[Boot] {message}");
            else Debug.LogWarning($"[Boot] {message}");
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
