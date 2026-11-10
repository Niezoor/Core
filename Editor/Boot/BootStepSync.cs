using System;
using System.Collections.Generic;
using System.Linq;
using Core.Bootstrap;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;

namespace Core.Editor
{
    /// <summary>
    /// Keeps <see cref="BootSettings"/> in step with the <see cref="BootStepAttribute"/> types of the player
    /// assemblies: adds the missing ones, sorts every phase. Runs after every compile, before Play, before a build,
    /// when the asset is inspected and from Core &gt; Boot. A project without BootSettings is left alone.
    /// </summary>
    [InitializeOnLoad]
    public static class BootStepSync
    {
        static BootStepSync()
        {
            EditorApplication.delayCall += SyncWhenIdle;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void SyncWhenIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += SyncWhenIdle;
                return;
            }

            Sync();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode) Sync();
        }

        [MenuItem("Core/Boot/Sync Boot Steps")]
        private static void SyncFromMenu()
        {
            if (!BootSettings.TryGet(out _))
                Debug.LogWarning("[Boot] The project has no BootSettings (Project Settings > Core > Boot).");
            else if (!Sync())
                Debug.Log("[Boot] Boot steps are up to date.");
        }

        /// <returns>Whether BootSettings changed.</returns>
        public static bool Sync()
        {
            if (!BootSettings.TryGet(out var settings)) return false;
            return Sync(settings);
        }

        public static bool Sync(BootSettings settings)
        {
            var added = new List<string>();
            if (!BootSteps.Merge(settings, Steps, added)) return false;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            if (added.Count > 0)
                Debug.Log($"[Boot] Added to {AssetDatabase.GetAssetPath(settings)}: {string.Join(", ", added)}", settings);
            return true;
        }

        /// <summary>Every <see cref="BootStepAttribute"/> type a build can contain - editor and test assemblies left
        /// out, their types would be missing from the player. Cached until the next compile reloads the domain.</summary>
        public static IReadOnlyDictionary<Type, BootStepAttribute> Steps => steps ??= FindSteps();

        private static IReadOnlyDictionary<Type, BootStepAttribute> steps;

        private static IReadOnlyDictionary<Type, BootStepAttribute> FindSteps()
        {
            var playerAssemblies = new HashSet<string>(CompilationPipeline
                .GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies).Select(a => a.name));
            var found = new Dictionary<Type, BootStepAttribute>();
            foreach (var type in TypeCache.GetTypesWithAttribute<BootStepAttribute>())
            {
                var attribute = (BootStepAttribute)Attribute.GetCustomAttribute(type, typeof(BootStepAttribute), false);
                if (attribute == null || !playerAssemblies.Contains(type.Assembly.GetName().Name)) continue;
                found[type] = attribute;
            }

            return found;
        }
    }

    /// <summary>Before the build collects Preloaded Assets, BootSettings among them.</summary>
    internal sealed class BootStepBuildProcessor : BuildPlayerProcessor
    {
        public override int callbackOrder => -200;

        public override void PrepareForBuild(BuildPlayerContext buildPlayerContext) => BootStepSync.Sync();
    }
}
