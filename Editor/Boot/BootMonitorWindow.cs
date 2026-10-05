using System.Linq;
using Core.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    /// <summary>
    /// What <see cref="Boot"/> ran and how long each step took: live in Play Mode, and the last run after it until the
    /// next Play starts.
    /// </summary>
    public sealed class BootMonitorWindow : EditorWindow
    {
        private Vector2 scroll;
        private GUIStyle rightAligned;

        [MenuItem("Core/Boot/Boot Monitor")]
        public static void Open()
        {
            var window = GetWindow<BootMonitorWindow>();
            window.titleContent = new GUIContent("Boot Monitor", EditorGUIUtility.IconContent("d_UnityEditor.ProfilerWindow").image);
            window.Show();
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying) Repaint();
        }

        private void OnGUI()
        {
            rightAligned ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight };

            if (!Boot.IsInitialized && Boot.Records.Count == 0)
            {
                EditorGUILayout.HelpBox(BootSettings.TryGet(out _)
                    ? "Enter Play Mode to see the boot."
                    : "The project has no BootSettings (Core > Boot > Setup Boot Scene).", MessageType.Info);
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Last run - cleared when Play Mode starts again.", MessageType.None);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawInitialization();
            EditorGUILayout.Space();
            DrawSplash();
            EditorGUILayout.EndScrollView();
        }

        private void DrawInitialization()
        {
            var initializers = Boot.Records.Where(r => r.Phase == BootPhase.Initializer).ToList();
            var services = Boot.Records.Where(r => r.Phase == BootPhase.Service).ToList();
            var totalMs = initializers.Sum(r => r.Duration) * 1000.0;
            var budgetMs = Boot.Settings ? Boot.Settings.InitializerBudgetMs : float.PositiveInfinity;

            EditorGUILayout.LabelField($"Initializers - {totalMs:0.0} ms" +
                                       (float.IsPositiveInfinity(budgetMs) ? "" : $" of {budgetMs:0} ms budget"),
                EditorStyles.boldLabel);
            if (totalMs > budgetMs)
            {
                EditorGUILayout.HelpBox("Over budget: every scene start, the editor's gameplay scenes included, waits " +
                                        "for the initializers.", MessageType.Warning);
            }

            if (initializers.Count == 0) EditorGUILayout.LabelField("None", EditorStyles.miniLabel);
            foreach (var record in initializers) DrawRow(record, false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Services (synchronous start only)", EditorStyles.boldLabel);
            if (services.Count == 0) EditorGUILayout.LabelField("None", EditorStyles.miniLabel);
            foreach (var record in services) DrawRow(record, false);
        }

        private void DrawSplash()
        {
            EditorGUILayout.LabelField("Splash", EditorStyles.boldLabel);
            if (double.IsNaN(Boot.SplashStartedAt))
            {
                EditorGUILayout.LabelField("Not run - the game started outside the boot scene.", EditorStyles.miniLabel);
                return;
            }

            var current = Boot.Records.LastOrDefault(r => r.Phase == BootPhase.Splash && r.Result == BootStepResult.Running);
            var label = Boot.SplashState switch
            {
                SplashState.Loading => current != null ? $"Loading: {current.Name}" + (current.Status != null ? $" ({current.Status})" : "") : "Loading",
                SplashState.Ready => "Loaded - waiting for Boot.Continue() or the minimum duration",
                SplashState.Failed => $"Failed: {Boot.FailedTask?.DisplayName}",
                _ => double.IsNaN(Boot.SplashLeftAt) ? "Stopped" : "Done",
            };
            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2f);
            EditorGUI.ProgressBar(rect, Boot.SplashProgress, $"{label} - {Boot.SplashProgress * 100f:0}%");

            DrawMilestone("Loaded after", Boot.SplashReadyAt);
            DrawMilestone("Left after", Boot.SplashLeftAt);
            if (Boot.IsRestart) EditorGUILayout.LabelField("This splash came from Boot.Restart().", EditorStyles.miniLabel);

            EditorGUILayout.Space(2f);
            foreach (var record in Boot.Records.Where(r => r.Phase == BootPhase.Splash)) DrawRow(record, true);

            if (Boot.SplashState == SplashState.Failed && Boot.Failure != null)
            {
                EditorGUILayout.HelpBox(Boot.Failure.Message, MessageType.Error);
            }

            if (!EditorApplication.isPlaying) return;

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(Boot.SplashState is not (SplashState.Loading or SplashState.Ready)))
                {
                    if (GUILayout.Button("Continue")) Boot.Continue();
                }

                using (new EditorGUI.DisabledScope(Boot.SplashState != SplashState.Failed))
                {
                    if (GUILayout.Button("Retry")) Boot.Retry();
                }

                if (GUILayout.Button("Restart")) Boot.Restart();
            }
        }

        private static void DrawMilestone(string label, double at)
        {
            if (double.IsNaN(at)) return;
            EditorGUILayout.LabelField(label, $"{at - Boot.SplashStartedAt:0.00} s");
        }

        private void DrawRow(BootStepRecord record, bool withProgress)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(Icon(record.Result), GUILayout.Width(20f), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                GUILayout.Label(new GUIContent(record.Name, record.Error ?? record.Result.ToString()));
                if (withProgress && record.Result == BootStepResult.Running)
                {
                    var rect = GUILayoutUtility.GetRect(80f, EditorGUIUtility.singleLineHeight, GUILayout.Width(80f));
                    EditorGUI.ProgressBar(rect, record.Progress, "");
                }

                GUILayout.Label(FormatDuration(record.Duration), rightAligned, GUILayout.Width(70f));
            }

            if (record.Error != null && record.Result != BootStepResult.Succeeded)
            {
                EditorGUILayout.LabelField(record.Error, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private static string FormatDuration(double seconds) =>
            seconds < 1.0 ? $"{seconds * 1000.0:0.0} ms" : $"{seconds:0.00} s";

        private static GUIContent Icon(BootStepResult result)
        {
            var name = result switch
            {
                BootStepResult.Succeeded => "TestPassed",
                BootStepResult.Failed or BootStepResult.TimedOut => "TestFailed",
                BootStepResult.Skipped => "TestInconclusive",
                BootStepResult.Cancelled => "TestIgnored",
                _ => "WaitSpin00",
            };
            return new GUIContent(EditorGUIUtility.IconContent(name).image, result.ToString());
        }
    }
}
