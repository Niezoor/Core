using System;
using System.Collections.Generic;
using System.Linq;
using Core.Utilities.Settings;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Editor
{
    /// <summary>
    /// Puts each settings asset where the build looks for it: [PreloadedSettings] in Player Settings &gt; Preloaded
    /// Assets, the rest in Addressables under <see cref="SettingsRegistry.AddressablesLabel"/> - never both, or the
    /// build carries two copies. Runs after every compile, when an asset is created, before every build and from
    /// Core &gt; Settings. Only existing assets are synced: a type nobody has used gets no asset.
    /// </summary>
    [InitializeOnLoad]
    public static class SettingsAssetSync
    {
        /// <summary>The Addressables group new async settings go to; an entry moved elsewhere is left there.</summary>
        public const string GroupName = "Settings";

        private const string Label = SettingsRegistry.AddressablesLabel;

        static SettingsAssetSync()
        {
            // A changed [PreloadedSettings] takes effect after the compile.
            EditorApplication.delayCall += SyncWhenIdle;
        }

        private static void SyncWhenIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += SyncWhenIdle;
                return;
            }

            SyncAll();
        }

        [MenuItem("Core/Settings/Sync Settings Assets")]
        public static void SyncAll()
        {
            foreach (var type in SettingsAssetDatabase.Types)
            {
                var all = SettingsAssetDatabase.FindAll(type);
                if (all.Count == 0) continue;

                var used = SettingsAssetDatabase.Find(type) ?? all[0];
                foreach (var duplicate in all)
                {
                    if (duplicate != used) Unregister(duplicate);
                }

                Sync(type, used);
            }
        }

        /// <summary>Puts <paramref name="asset"/> where the build loads <paramref name="type"/> from.</summary>
        public static void Sync(Type type, SettingsAsset asset)
        {
            if (SettingsRegistry.IsPreloaded(type))
            {
                RemoveFromAddressables(asset, "it is [PreloadedSettings]");
                AddToPreloadedAssets(asset);
            }
            else
            {
                RemoveFromPreloadedAssets(asset, "it loads asynchronously");
                AddToAddressables(type, asset);
            }
        }

        /// <summary>The build carries <paramref name="asset"/> - preloaded or under the Addressables label.</summary>
        public static bool IsRegistered(SettingsAsset asset) =>
            PlayerSettings.GetPreloadedAssets().Contains(asset) || FindEntry(asset)?.labels.Contains(Label) == true;

        /// <summary>Settings that load asynchronously exist, so something must call LoadAllAsync.</summary>
        public static bool HasAsyncAssets() =>
            SettingsAssetDatabase.Types.Any(t => !SettingsRegistry.IsPreloaded(t) && SettingsAssetDatabase.Find(t));

        private static void Unregister(SettingsAsset duplicate)
        {
            const string reason = "another asset of its type is the one in use";
            RemoveFromPreloadedAssets(duplicate, reason);
            RemoveFromAddressables(duplicate, reason);
        }

        private static void AddToPreloadedAssets(SettingsAsset asset)
        {
            var preloaded = PlayerSettings.GetPreloadedAssets().ToList();
            if (preloaded.Contains(asset)) return;

            preloaded.Add(asset);
            PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
            Debug.Log($"[Settings] Added {AssetDatabase.GetAssetPath(asset)} to Preloaded Assets.", asset);
        }

        private static void RemoveFromPreloadedAssets(SettingsAsset asset, string reason)
        {
            var preloaded = PlayerSettings.GetPreloadedAssets().ToList();
            if (preloaded.RemoveAll(o => o == asset) == 0) return;

            PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
            Debug.Log($"[Settings] Removed {AssetDatabase.GetAssetPath(asset)} from Preloaded Assets - {reason}.", asset);
        }

        private static AddressableAssetEntry FindEntry(Object asset)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            return settings ? settings.FindAssetEntry(Guid(asset)) : null;
        }

        private static void AddToAddressables(Type type, SettingsAsset asset)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (!settings)
            {
                settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                Debug.Log("[Settings] Created the Addressables settings - async settings load from Addressables.");
            }

            var entry = settings.FindAssetEntry(Guid(asset));
            var changed = false;
            if (entry == null)
            {
                var group = settings.FindGroup(GroupName) ??
                            settings.CreateGroup(GroupName, false, false, true, null,
                                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                entry = settings.CreateOrMoveEntry(Guid(asset), group, false, false);
                // Loading goes by label, so the address is only a stable name for people - not the file's path.
                entry.address = type.FullName;
                changed = true;
            }

            if (!entry.labels.Contains(Label))
            {
                entry.SetLabel(Label, true, true, false);
                changed = true;
            }

            if (!changed) return;

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true, true);
            AssetDatabase.SaveAssetIfDirty(entry.parentGroup);
            AssetDatabase.SaveAssetIfDirty(settings);
            Debug.Log($"[Settings] Put {AssetDatabase.GetAssetPath(asset)} in Addressables " +
                      $"({entry.parentGroup.Name}, label \"{Label}\").", asset);
        }

        private static void RemoveFromAddressables(SettingsAsset asset, string reason)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings ? settings.FindAssetEntry(Guid(asset)) : null;
            if (entry == null) return;

            var group = entry.parentGroup;
            settings.RemoveAssetEntry(entry.guid);
            AssetDatabase.SaveAssetIfDirty(group);
            AssetDatabase.SaveAssetIfDirty(settings);
            Debug.Log($"[Settings] Removed {AssetDatabase.GetAssetPath(asset)} from Addressables - {reason}.", asset);
        }

        private static string Guid(Object asset) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));

        /// <summary>
        /// Warns about what the build would get wrong although every asset is in its place: references that give the
        /// build a second copy of a settings asset, and preloaded settings pulling heavy content into the launch.
        /// </summary>
        public static void Validate()
        {
            var preloaded = new Dictionary<string, SettingsAsset>();
            var asynchronous = new Dictionary<string, SettingsAsset>();
            foreach (var type in SettingsAssetDatabase.Types)
            {
                var asset = SettingsAssetDatabase.Find(type);
                if (!asset) continue;
                (SettingsRegistry.IsPreloaded(type) ? preloaded : asynchronous)[AssetDatabase.GetAssetPath(asset)] = asset;
            }

            if (preloaded.Count == 0 && asynchronous.Count == 0) return;

            var dependencies = new DependencyCache();
            WarnAboutAddressablesCopies(preloaded, dependencies);
            WarnAboutPlayerCopies(asynchronous, dependencies);
            WarnAboutHeavyPreloads(preloaded, dependencies);
        }

        // A bundle gets its own copy of any non-Addressables asset it references - not the one Instance returns.
        private static void WarnAboutAddressablesCopies(Dictionary<string, SettingsAsset> preloaded,
            DependencyCache dependencies)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (preloaded.Count == 0 || !settings) return;

            var entries = new List<AddressableAssetEntry>();
            settings.GetAllAssets(entries, false);
            foreach (var entry in entries)
            {
                foreach (var path in dependencies.Of(entry.AssetPath).Where(preloaded.ContainsKey))
                {
                    Debug.LogWarning($"[Settings] {entry.AssetPath} (Addressables) references the preloaded {path}: " +
                                     "its bundle gets a copy of its own, which is not what Instance returns. Read it " +
                                     "through Instance instead of a serialized reference.", preloaded[path]);
                }
            }
        }

        // The player's scenes and preloaded assets get their own copy of an Addressables asset they reference directly.
        private static void WarnAboutPlayerCopies(Dictionary<string, SettingsAsset> asynchronous,
            DependencyCache dependencies)
        {
            if (asynchronous.Count == 0) return;

            var roots = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)
                .Concat(PlayerSettings.GetPreloadedAssets().Where(o => o).Select(AssetDatabase.GetAssetPath));
            foreach (var root in roots)
            {
                foreach (var path in dependencies.Of(root).Where(asynchronous.ContainsKey))
                {
                    Debug.LogWarning($"[Settings] {root} (in the player) references {path}, which loads from " +
                                     "Addressables: the player gets a copy of its own, which is not what Instance " +
                                     "returns. Read it through Instance instead of a serialized reference.",
                        asynchronous[path]);
                }
            }
        }

        private static void WarnAboutHeavyPreloads(Dictionary<string, SettingsAsset> preloaded,
            DependencyCache dependencies)
        {
            foreach (var pair in preloaded)
            {
                var heavy = dependencies.Of(pair.Key).Where(IsHeavy).ToList();
                if (heavy.Count == 0) continue;

                Debug.LogWarning($"[Settings] The preloaded {pair.Key} pulls {heavy.Count} textures, sounds, meshes " +
                                 $"or prefabs into the launch (e.g. {string.Join(", ", heavy.Take(3))}). Reference " +
                                 "them from async settings, or drop [PreloadedSettings].", pair.Value);
            }
        }

        private static bool IsHeavy(string path)
        {
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            return type != null && (typeof(Texture).IsAssignableFrom(type) || typeof(AudioClip).IsAssignableFrom(type) ||
                                    typeof(Mesh).IsAssignableFrom(type) || type == typeof(GameObject));
        }

        /// <summary>Everything an asset pulls into a build, without going into referenced scenes (a SceneRef).</summary>
        private sealed class DependencyCache
        {
            private readonly Dictionary<string, string[]> direct = new();

            public HashSet<string> Of(string root)
            {
                var found = new HashSet<string>();
                var queue = new Queue<string>();
                queue.Enqueue(root);
                while (queue.Count > 0)
                {
                    var path = queue.Dequeue();
                    if (!direct.TryGetValue(path, out var dependencies))
                        direct[path] = dependencies = AssetDatabase.GetDependencies(path, false);

                    foreach (var dependency in dependencies)
                    {
                        // A scene loads on its own; what it holds is not carried by whoever points at it.
                        if (dependency == root || !found.Add(dependency) || dependency.EndsWith(".unity")) continue;
                        queue.Enqueue(dependency);
                    }
                }

                return found;
            }
        }
    }

    /// <summary>Syncs before Addressables builds its content (its processor runs at order 1), then validates.</summary>
    internal sealed class SettingsAssetBuildProcessor : BuildPlayerProcessor
    {
        public override int callbackOrder => -100;

        public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
        {
            SettingsAssetSync.SyncAll();
            SettingsAssetSync.Validate();
        }
    }
}
