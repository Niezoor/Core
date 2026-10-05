using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Utilities.Settings;
using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    /// <summary>
    /// The editor's side of <see cref="SettingsRegistry"/>: finds the asset of a <see cref="SettingsAsset"/> type
    /// anywhere under Assets - by type, so the file can be renamed and moved - and creates it on first use.
    /// </summary>
    [InitializeOnLoad]
    public static class SettingsAssetDatabase
    {
        /// <summary>Where a new asset goes unless its type has [SettingsPath]; it can be moved afterwards.</summary>
        public const string DefaultFolder = "Assets/Settings";

        // Types with no asset, until the project changes: windows ask on every repaint.
        private static readonly HashSet<Type> missing = new();
        private static readonly HashSet<Type> warnedDuplicates = new();
        private static readonly HashSet<Type> warnedImporting = new();

        static SettingsAssetDatabase()
        {
            SettingsRegistry.EditorResolver = Resolve;
            EditorApplication.projectChanged += missing.Clear;
        }

        /// <summary>Every concrete settings type in the project.</summary>
        public static IEnumerable<Type> Types =>
            TypeCache.GetTypesDerivedFrom<SettingsAsset>().Where(t => !t.IsAbstract && !t.ContainsGenericParameters);

        /// <summary>The asset of <paramref name="type"/>, or null when the project has none.</summary>
        public static SettingsAsset Find(Type type)
        {
            if (missing.Contains(type)) return null;

            var found = FindAll(type);
            switch (found.Count)
            {
                case 0:
                    missing.Add(type);
                    return null;
                case 1:
                    return found[0];
                default:
                    return PickBound(type, found);
            }
        }

        /// <summary>The asset of <paramref name="type"/>, created when the project has none (null only mid-import).</summary>
        public static SettingsAsset GetOrCreate(Type type) => Resolve(type, true);

        /// <summary>Every asset of exactly <paramref name="type"/> under Assets; more than one is a mistake.</summary>
        public static List<SettingsAsset> FindAll(Type type)
        {
            var result = new List<SettingsAsset>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{type.Name}", new[] { "Assets" }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<SettingsAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset && asset.GetType() == type) result.Add(asset);
            }

            if (result.Count > 0) missing.Remove(type);
            return result;
        }

        private static SettingsAsset Resolve(Type type, bool create)
        {
            var asset = Find(type);
            return asset || !create ? asset : Create(type);
        }

        // A duplicated file: keep the one the build already carries, so nothing changes under the game's feet.
        private static SettingsAsset PickBound(Type type, List<SettingsAsset> found)
        {
            var bound = found.FirstOrDefault(SettingsAssetSync.IsRegistered) ?? found[0];
            if (warnedDuplicates.Add(type))
            {
                Debug.LogWarning($"[Settings] {found.Count} assets of {type.Name}: " +
                                 $"{string.Join(", ", found.Select(AssetDatabase.GetAssetPath))}. Using " +
                                 $"{AssetDatabase.GetAssetPath(bound)} - delete the others.", bound);
            }

            return bound;
        }

        private static SettingsAsset Create(Type type)
        {
            // Mid-import an existing asset may not be listed yet, and creating assets is not allowed there.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                if (warnedImporting.Add(type))
                    Debug.LogWarning($"[Settings] {type.Name} was read while the editor was importing; its asset is " +
                                     "looked up (or created) on the next read.");
                return null;
            }

            var folder = (type.GetCustomAttribute<SettingsPathAttribute>()?.Path ?? DefaultFolder).TrimEnd('/');
            EnsureFolder(folder);
            // Unique, so an unreadable file of the same name (a script gone missing) is never overwritten.
            var path = AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/{ObjectNames.NicifyVariableName(type.Name)}.asset");

            var asset = (SettingsAsset)ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssetIfDirty(asset);
            missing.Remove(type);
            Debug.Log($"[Settings] Created {path}", asset);

            SettingsAssetSync.Sync(type, asset);
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var slash = folder.LastIndexOf('/');
            if (slash < 0) throw new ArgumentException($"Settings folder must be under Assets: {folder}");

            var parent = folder.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }
    }
}
