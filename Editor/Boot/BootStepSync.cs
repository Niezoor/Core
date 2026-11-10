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
    /// Writes the <see cref="BootStepAttribute"/> types of the player assemblies into <see cref="BootSettings"/> -
    /// the steps, their order and their configuration all come from code. Runs after every compile, before Play,
    /// before a build, when the asset is inspected and from Core &gt; Boot. A project without BootSettings is left
    /// alone.
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
            var removed = new List<string>();
            var changed = BootSteps.Merge(settings, Steps, added, removed);

            // A step whose class is gone (assembly removed, class renamed) left data nobody can load.
            if (SerializationUtility.HasManagedReferencesWithMissingTypes(settings))
            {
                SerializationUtility.ClearAllManagedReferencesWithMissingTypes(settings);
                changed = true;
            }

            if (!changed) return false;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            var path = AssetDatabase.GetAssetPath(settings);
            if (added.Count > 0) Debug.Log($"[Boot] Steps added to {path}: {string.Join(", ", added)}", settings);
            if (removed.Count > 0)
                Debug.LogWarning($"[Boot] Steps removed from {path}, their classes have no [BootStep]: " +
                                 $"{string.Join(", ", removed)}. Mark a class [BootStep] to run it again.", settings);
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
