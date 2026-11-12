using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Bootstrap;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Core.Editor
{
    /// <summary>
    /// Keeps the scene holding <see cref="BootScene"/> first in Build Settings. Never creates a scene on its own: a
    /// missing one is reported in the BootSettings inspector, where it can be added to a chosen location, and fails
    /// the build. Play Mode doesn't check anything - a gameplay scene runs without the boot scene. Does nothing in a
    /// project without <see cref="BootSettings"/>.
    /// </summary>
    public static class BootSceneSetup
    {
        public const string DefaultScenePath = "Assets/Scenes/Boot.unity";

        // Editor/Boot/BootSceneTemplate.unity.meta
        private const string TemplateGuid = "24b92c9007aa42128019b7b68da13df8";

        public enum Status
        {
            /// <summary>No BootSettings: the project doesn't use the bootstrap.</summary>
            NotUsed,
            Ok,
            /// <summary>A scene with BootScene exists but is not the first enabled one in Build Settings.</summary>
            NotFirst,
            Missing,
        }

        [MenuItem("Core/Boot/Add Boot Scene...")]
        private static void AddFromMenu()
        {
            if (!BootSettings.TryGet(out _))
            {
                // The explicit request is the opt-in: create the settings it needs.
                _ = BootSettings.Instance;
            }

            var done = GetStatus(out _) == Status.Missing ? AddBootScene(out var message) : FixOrder(out message);
            if (done) Debug.Log($"[Boot] {message}");
            else Debug.LogWarning($"[Boot] {message}");
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

        /// <summary>Moves an existing boot scene to the top of Build Settings. False when there is none.</summary>
        public static bool FixOrder(out string message)
        {
            switch (GetStatus(out var path))
            {
                case Status.NotFirst:
                    MoveToFront(path);
                    message = $"Moved {path} to the top of Build Settings - it is the boot scene.";
                    return true;
                case Status.Ok:
                    message = $"{path} is the boot scene.";
                    return true;
                case Status.NotUsed:
                    message = "The project has no BootSettings; nothing to set up.";
                    return true;
                default:
                    message = "No scene has a BootScene component.";
                    return false;
            }
        }

        /// <summary>
        /// Asks where to put a new boot scene (a copy of the package's template), creates it and puts it first in
        /// Build Settings. False when cancelled or it could not be created; <paramref name="message"/> says what happened.
        /// </summary>
        public static bool AddBootScene(out string message)
        {
            var folder = Path.GetDirectoryName(DefaultScenePath)?.Replace('\\', '/');
            var initial = !string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder) ? folder : "Assets";
            var path = EditorUtility.SaveFilePanelInProject("Add Boot Scene",
                Path.GetFileNameWithoutExtension(DefaultScenePath), "unity",
                "Where to create the boot scene (a camera and the BootScene component).", initial);
            if (string.IsNullOrEmpty(path))
            {
                message = "Adding the boot scene was cancelled.";
                return false;
            }

            if (!CreateBootScene(path, out message)) return false;
            MoveToFront(path);
            message = $"Created {path} as the boot scene and put it first in Build Settings.";
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

        private static void MoveToFront(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != scenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// Copies the package's template (a camera and the BootScene marker) rather than building a scene in the
        /// editor, which Unity refuses while an untitled scene is open - the usual state of a fresh project.
        /// </summary>
        private static bool CreateBootScene(string path, out string message)
        {
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

            if (File.Exists(path))
            {
                message = $"{path} already exists - pick another file name.";
                return false;
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

    /// <summary>A build without a boot scene fails; one that is only out of order is fixed and the build restarted.</summary>
    internal sealed class BootSceneBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var status = BootSceneSetup.GetStatus(out _);
            if (status is BootSceneSetup.Status.NotUsed or BootSceneSetup.Status.Ok) return;

            if (status == BootSceneSetup.Status.Missing)
            {
                throw new BuildFailedException("[Boot] The project has no boot scene (a scene with a BootScene " +
                                               "component). Add one in Project Settings > Core > Boot or with " +
                                               "Core > Boot > Add Boot Scene.");
            }

            // The build's scene list is fixed before this runs, so fixing the order means starting the build again.
            BootSceneSetup.FixOrder(out var message);
            throw new BuildFailedException($"[Boot] The boot scene was not first in Build Settings. {message} Start the build again.");
        }
    }
}
