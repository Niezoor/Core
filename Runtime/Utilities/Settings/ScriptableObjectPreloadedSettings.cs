using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
#if !UNITY_EDITOR
using System.Linq;
#endif

namespace Core.Utilities.Settings
{
    public abstract class ScriptableObjectPreloadedSettings<T> : ScriptableObject
        where T : ScriptableObjectPreloadedSettings<T>
    {
        private static T instance;

        public static T Instance
        {
            get
            {
                if (!instance)
                {
                    GetOrCreateDefault();
                }

                return instance;
            }
        }


        private static void GetOrCreateDefault()
        {
#if UNITY_EDITOR
            EditorBuildSettings.TryGetConfigObject($"com.core.{typeof(T).Name}", out instance);
            if (!instance) TryLoadAsset();
            if (!instance) CreateAndSaveAsset();
            if (instance)
            {
                EditorBuildSettings.AddConfigObject($"com.core.{typeof(T).Name}", instance, true);
                AddToPreloadedAssets();
            }

            ResetOnPlayModeChange();
#else
            instance = Resources.FindObjectsOfTypeAll<T>().FirstOrDefault();
#endif
        }

#if UNITY_EDITOR
        private static void AddToPreloadedAssets()
        {
            if (!instance) return;
            var preloadedAssets = new List<Object>(PlayerSettings.GetPreloadedAssets());
            if (preloadedAssets.Contains(instance)) return;
            preloadedAssets.Add(instance);
            PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
        }

        private static void ResetOnPlayModeChange()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChange;
            EditorApplication.playModeStateChanged += OnPlayModeChange;
        }

        private static void OnPlayModeChange(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode ||
                change == PlayModeStateChange.EnteredEditMode)
            {
                instance = null;
            }
        }

        private static string GetAssetPath()
        {
            var type = typeof(T);
            var attribute = (SettingsPathAttribute)type.GetCustomAttribute(typeof(SettingsPathAttribute));
            var path = attribute != null ? attribute.Path : "Assets/Settings/";
            return Path.Combine(path, ObjectNames.NicifyVariableName(type.Name) + ".asset");
        }

        private static void CreateAndSaveAsset()
        {
            if (instance) return;
            var path = GetAssetPath();
            instance = CreateInstance<T>();
            instance.name = Path.GetFileNameWithoutExtension(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
        }


        private static void TryLoadAsset()
        {
            var path = GetAssetPath();
            instance = AssetDatabase.LoadAssetAtPath<T>(path);
        }

        protected static ScriptableObjectSettingsProvider<T> GetDefaultSettings(string settingsName)
        {
            return new ScriptableObjectSettingsProvider<T>(Instance, settingsName);
        }
#endif
    }
}