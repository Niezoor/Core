using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.UISystem.Editor
{
    /// <summary>
    /// Keeps the screen registry in <see cref="UISystemSettings"/> filled: after every compilation each
    /// <see cref="UIScreen"/> without a view gets the UXML with its class name, wherever it is.
    /// </summary>
    [InitializeOnLoad]
    internal static class ScreenRegistry
    {
        /// <summary>Set by the "UI Screen" create menu: the next sync creates the settings asset if needed.</summary>
        internal const string CreateSettingsKey = "Core.UISystem.CreateSettings";

        static ScreenRegistry()
        {
            EditorApplication.delayCall += () => Sync(false);
        }

        /// <summary>Concrete screen classes, by full name.</summary>
        internal static IReadOnlyList<Type> ScreenTypes => TypeCache.GetTypesDerivedFrom<UIScreen>()
            .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

        [MenuItem("Core/UI System/Sync Screens")]
        private static void SyncFromMenu()
        {
            Sync(true, true);
        }

        internal static void Sync(bool create, bool report = false)
        {
            create |= SessionState.GetBool(CreateSettingsKey, false);
            var settings = (UISystemSettings)(create
                ? SettingsAssetDatabase.GetOrCreate(typeof(UISystemSettings))
                : SettingsAssetDatabase.Find(typeof(UISystemSettings)));
            if (!settings) return;
            SessionState.EraseBool(CreateSettingsKey);

            var added = new List<string>();
            var missing = new List<string>();
            foreach (var type in ScreenTypes)
            {
                if (HasCodeView(type)) continue;
                var entry = settings.FindEntry(type);
                if (entry != null && entry.View) continue;

                var view = FindUxml(type.Name);
                if (!view)
                {
                    missing.Add(type.Name);
                    continue;
                }

                if (entry == null) settings.EditableScreens.Add(new ScreenEntry(type, view));
                else entry.SetView(view);
                added.Add($"{type.Name} → {AssetDatabase.GetAssetPath(view)}");
            }

            var stale = settings.Screens.Where(e => e.Type == null).Select(e => e.TypeName).ToList();
            if (added.Count > 0)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssetIfDirty(settings);
                Debug.Log($"[UISystem] Paired screens with their UXML:\n{string.Join("\n", added)}", settings);
            }

            if (!report) return;
            if (missing.Count > 0)
            {
                Debug.LogWarning($"[UISystem] No view for: {string.Join(", ", missing)}. Create a UXML with the class " +
                                 "name or assign one in Project Settings > Core > UI System.", settings);
            }

            if (stale.Count > 0)
            {
                Debug.LogWarning($"[UISystem] Registered screens that no longer exist: {string.Join(", ", stale)}.",
                    settings);
            }

            if (added.Count == 0 && missing.Count == 0 && stale.Count == 0) Debug.Log("[UISystem] Screens are in sync.", settings);
        }

        /// <summary>The screen builds its view in code, so it needs no UXML.</summary>
        private static bool HasCodeView(Type type)
        {
            var method = type.GetMethod("CreateView", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            return method != null && method.DeclaringType != typeof(UIScreen);
        }

        private static VisualTreeAsset FindUxml(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:VisualTreeAsset {name}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            }

            return null;
        }
    }
}
