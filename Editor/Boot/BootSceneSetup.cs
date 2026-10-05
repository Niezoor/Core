using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Bootstrap;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Editor
{
    /// <summary>
    /// Keeps the scene holding <see cref="BootScene"/> first in Build Settings, creating it when the project has none.
    /// Does nothing in a project without <see cref="BootSettings"/>. Checked before Play (fixing what it can, never
    /// blocking Play), before a build (stopping it) and from Core &gt; Boot &gt; Setup Boot Scene - never on compile,
    /// so writing code never creates scenes.
    /// </summary>
    public static class BootSceneSetup
    {
        public const string DefaultScenePath = "Assets/Scenes/Boot.unity";

        // Editor/Boot/BootSceneTemplate.unity.meta
        private const string TemplateGuid = "24b92c9007aa42128019b7b68da13df8";

        /// <summary>Scene names that may already be a boot scene of an older setup, compared case-insensitively.</summary>
        private static readonly string[] CandidateNames = { "Boot", "Launcher", "Loader" };

        public enum Status
        {
            /// <summary>No BootSettings: the project doesn't use the bootstrap.</summary>
            NotUsed,
            Ok,
            /// <summary>A scene with BootScene exists but is not the first enabled one in Build Settings.</summary>
            NotFirst,
            Missing,
        }

        [InitializeOnLoadMethod]
        private static void HookPlayMode()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Core/Boot/Setup Boot Scene")]
        private static void SetupFromMenu()
        {
            if (!BootSettings.TryGet(out _))
            {
                // The explicit request is the opt-in: create the settings it needs.
                _ = BootSettings.Instance;
            }

            if (Ensure(true, out var message)) Debug.Log($"[Boot] {message}");
            else Debug.LogError($"[Boot] {message}");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode) return;

            // Play goes on either way: a gameplay scene runs without the boot scene, and the test runner enters Play
            // from scenes of its own. The build check is what stops a broken setup from shipping.
            if (!Ensure(!Application.isBatchMode, out var message)) Debug.LogError($"[Boot] {message}");
        }

        /// <summary>Where the project stands, without changing anything.</summary>
        public static Status GetStatus(out string bootScenePath)
        {
            bootScenePath = null;
            if (!BootSettings.TryGet(out _)) return Status.NotUsed;

            var first = EditorBuildSettings.scenes.FirstOrDefault(s => s.enabled && File.Exists(s.path));
            if (first != null && HasMarker(first.path))
            {
                bootScenePath = first.path;
                return Status.Ok;
            }

            bootScenePath = FindMarkedScenes().FirstOrDefault();
            return bootScenePath != null ? Status.NotFirst : Status.Missing;
        }

        /// <summary>
        /// Makes the boot scene the first enabled scene in Build Settings, moving, marking or creating one as needed.
        /// </summary>
        /// <param name="interactive">May ask before turning an existing Boot/Launcher/Loader scene into the boot
        /// scene; without asking such a scene is left alone and this fails.</param>
        /// <returns>False when it could not, or the user cancelled; <paramref name="message"/> says why.</returns>
        public static bool Ensure(bool interactive, out string message)
        {
            switch (GetStatus(out var path))
            {
                case Status.NotUsed:
                    message = "The project has no BootSettings; nothing to set up.";
                    return true;
                case Status.Ok:
                    message = $"{path} is the boot scene.";
                    return true;
                case Status.NotFirst:
                    MoveToFront(path);
                    message = $"Moved {path} to the top of Build Settings - it is the boot scene.";
                    Debug.Log($"[Boot] {message}");
                    return true;
            }

            var candidate = FindCandidateScene();
            if (candidate != null)
            {
                if (!interactive)
                {
                    message = $"Build Settings has no boot scene. {candidate} looks like one but has no BootScene " +
                              "component - add it (or run Core > Boot > Setup Boot Scene) and build again.";
                    return false;
                }

                var choice = EditorUtility.DisplayDialogComplex("Boot scene",
                    $"The project has no scene with a BootScene component, but {candidate} looks like a boot scene.\n\n" +
                    "Use it? A BootScene object is added to it and it goes first in Build Settings. Check that " +
                    "nothing else in it loads the next scene - Boot does that now.",
                    $"Use {Path.GetFileNameWithoutExtension(candidate)}", "Cancel", "Create a new Boot scene");

                switch (choice)
                {
                    case 0:
                        if (!AddMarker(candidate, out message)) return false;
                        MoveToFront(candidate);
                        message = $"Added BootScene to {candidate} and made it the boot scene.";
                        Debug.Log($"[Boot] {message}");
                        return true;
                    case 1:
                        message = "Boot scene setup cancelled.";
                        return false;
                }
            }

            if (!CreateBootScene(out var created, out message)) return false;
            MoveToFront(created);
            message = $"Created {created} as the boot scene and put it first in Build Settings.";
            Debug.Log($"[Boot] {message}");
            return true;
        }

        private static bool HasMarker(string scenePath)
        {
            var guid = MarkerScriptGuid();
            if (guid == null || !File.Exists(scenePath)) return false;

            // A text scene names every script it uses by GUID; no need to open it.
            var text = File.ReadAllText(scenePath);
            return text.IndexOf($"guid: {guid}", StringComparison.Ordinal) >= 0;
        }

        private static string MarkerScriptGuid()
        {
            var script = MonoImporter.GetAllRuntimeMonoScripts().FirstOrDefault(s => s && s.GetClass() == typeof(BootScene));
            if (!script) return null;
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out var guid, out long _) ? guid : null;
        }

        private static IEnumerable<string> FindMarkedScenes()
        {
            var template = AssetDatabase.GUIDToAssetPath(TemplateGuid);
            var buildScenes = EditorBuildSettings.scenes.Select(s => s.path);
            var projectScenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath);
            return buildScenes.Concat(projectScenes).Distinct().Where(p => p != template && HasMarker(p));
        }

        private static string FindCandidateScene()
        {
            var buildScenes = EditorBuildSettings.scenes.Select(s => s.path);
            var projectScenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath);
            return buildScenes.Concat(projectScenes).Distinct()
                .Where(File.Exists)
                .FirstOrDefault(p => CandidateNames.Any(n =>
                    string.Equals(Path.GetFileNameWithoutExtension(p), n, StringComparison.OrdinalIgnoreCase)));
        }

        private static void MoveToFront(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != scenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static bool AddMarker(string scenePath, out string message)
        {
            var scene = SceneManager.GetSceneByPath(scenePath);
            var openedHere = !scene.isLoaded;
            try
            {
                if (openedHere) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                var host = new GameObject("[Boot]");
                SceneManager.MoveGameObjectToScene(host, scene);
                host.AddComponent<BootScene>();

                if (!EditorSceneManager.SaveScene(scene))
                {
                    message = $"Could not save {scenePath}.";
                    return false;
                }

                message = null;
                return true;
            }
            catch (Exception exception)
            {
                message = $"Could not add BootScene to {scenePath}: {exception.Message}";
                return false;
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// Copies the package's template (a camera and the BootScene marker) rather than building a scene in the
        /// editor, which Unity refuses while an untitled scene is open - the usual state of a fresh project.
        /// </summary>
        private static bool CreateBootScene(out string path, out string message)
        {
            path = AssetDatabase.GenerateUniqueAssetPath(DefaultScenePath);
            var template = AssetDatabase.GUIDToAssetPath(TemplateGuid);
            if (string.IsNullOrEmpty(template))
            {
                // Files updated with auto refresh off (e.g. under Hot Reload) are on disk but not imported yet.
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                template = AssetDatabase.GUIDToAssetPath(TemplateGuid);
            }

            if (string.IsNullOrEmpty(template))
            {
                message = "The boot scene template (Editor/Boot/BootSceneTemplate.unity) is missing from the Core " +
                          "package - reimport it (Assets > Refresh) and try again.";
                return false;
            }

            var folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder((Path.GetDirectoryName(folder) ?? "Assets").Replace('\\', '/'), Path.GetFileName(folder));
            }

            if (!AssetDatabase.CopyAsset(template, path))
            {
                message = $"Could not copy {template} to {path}.";
                return false;
            }

            message = null;
            return true;
        }
    }

    /// <summary>
    /// The build's scene list is fixed before this runs, so a missing boot scene is set up and the build stopped,
    /// to be started again with it.
    /// </summary>
    internal sealed class BootSceneBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var status = BootSceneSetup.GetStatus(out _);
            if (status is BootSceneSetup.Status.NotUsed or BootSceneSetup.Status.Ok) return;

            var fixedIt = BootSceneSetup.Ensure(!Application.isBatchMode, out var message);
            throw new BuildFailedException(fixedIt
                ? $"[Boot] The boot scene was not first in Build Settings. {message} Start the build again."
                : $"[Boot] {message}");
        }
    }
}
