using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Core.Utilities
{
    public abstract class SoManagerAbstract : ScriptableObject
    {
    }

    public abstract class SoManager<T> : SoManagerAbstract where T : SoManagerAbstract
    {
        private static T instance;

        public static string FileName => typeof(T).Name;
        public static string ConfigName => "SoManagers." + FileName;

        [ShowInInspector, PropertyOrder(-10)]
        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    GetOrCreateSettings();
                }

                return instance;
            }
        }

        protected virtual void OnEnable()
        {
            if (instance != null)
            {
                if (instance != this)
                {
                    Debug.LogError(
                        $"Multiple instances of ScriptableObjectManager<{FileName}> found. This is not allowed and might lead to unexpected behaviours.");
                }

                return;
            }

            Debug.Log($"Activate {FileName} instance");
            instance = this as T;
#if UNITY_EDITOR
            TryAddToPreloadedAssets();
#endif
        }

        public static void GetOrCreateSettings()
        {
#if UNITY_EDITOR
            if (Application.isEditor)
            {
                if (!EditorBuildSettings.TryGetConfigObject(ConfigName, out instance))
                {
                    FindSettingsAsset();
                    if (instance == null)
                    {
                        CreateSettings();
                    }

                    EditorBuildSettings.AddConfigObject(ConfigName, instance, true);
                }

                TryAddToPreloadedAssets();
                return;
            }
#endif
            instance = FindObjectOfType<T>();
        }

#if UNITY_EDITOR
        private static void TryAddToPreloadedAssets()
        {
            var preloadedAssets = PlayerSettings.GetPreloadedAssets().ToList();
            if (!preloadedAssets.Contains(instance))
            {
                Debug.Log($"Adding {instance} to preloaded assets", instance);
                preloadedAssets.Add(instance);
            }

            PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
        }

        private static void FindSettingsAsset()
        {
            var assets = AssetDatabase.FindAssets($"t:{typeof(T)}");
            if (assets.Length > 0)
            {
                if (assets.Length > 1)
                {
                    Debug.LogError($"Please keep one copy of the {typeof(T)} in the project.");
                }

                var assetPath = AssetDatabase.GUIDToAssetPath(assets[0]);
                instance = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            }
        }

        private static void CreateSettings()
        {
            Debug.Log($"Settings '{ConfigName}' not found - create");
            var path = EditorUtility.SaveFilePanelInProject("Save Settings", FileName, "asset",
                $"Please enter a filename to save the projects {FileName} to.");

            if (string.IsNullOrEmpty(path))
                return;

            instance = CreateInstance<T>();
            instance.name = FileName;
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
        }
#endif
    }
}