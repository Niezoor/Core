using System;
using System.IO;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#if UNITY_EDITOR
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor;
#endif

namespace Core.Utilities.Settings
{
    public static class SettingsConst
    {
        public const string AddressableGroupName = "SoSettings";
    }

    /// <summary>
    /// Globally accessed settings loaded from Addressable.
    /// Getting Instance is synchronous which is not supported on web platforms.
    /// Call LoadAsync() to load asynchronously.
    /// Example code of creating settings provider to display settings in PlayerSettings panel:
    /// #if UNITY_EDITOR
    /// [SettingsProvider]
    /// public static SettingsProvider CreateSettingsProvider() => GetDefaultSettings(settingsName);
    /// #endif
    /// </summary>
    /// <typeparam name="T">Class of the settings</typeparam>
    [HideMonoScript]
    public class SoSettings<T> : ScriptableObject where T : SoSettings<T>
    {
        protected static T instance;

        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    GetOrCreateDefault();
                }

                return instance;
            }
        }

        public static bool HasInstance => instance != null;

        protected virtual void OnEnable()
        {
            if (instance == null)
            {
                instance = (T)this;
            }
        }

        public static void LoadAsync(Action onFinish = null)
        {
            var handle = Addressables.LoadAssetAsync<T>(typeof(T).Name);
            if (handle.IsValid())
            {
                if (handle.IsDone)
                {
                    Debug.Log($"Asset is loaded: {typeof(T).Name}");
                    OnLoadingFinish(handle);
                }
                else
                {
                    Debug.Log($"Asset loading async: {typeof(T).Name}");
                    handle.Completed += OnLoadingFinish;
                }
            }
            else
            {
                onFinish?.Invoke();
            }

            return;

            void OnLoadingFinish(AsyncOperationHandle<T> handle)
            {
                instance = handle.Result;
                Debug.Log($"Load settings {instance}");
                onFinish?.Invoke();
#if UNITY_EDITOR
                ResetOnPlayModeChange();
#endif
            }
        }

        private static void GetOrCreateDefault()
        {
            LoadFromAddressable();
            if (instance == null)
            {
                CreateDefault();
            }
        }

        private static void LoadFromAddressable()
        {
            var handle = Addressables.LoadAssetAsync<T>(typeof(T).Name);
            if (handle.IsValid())
            {
                instance = handle.WaitForCompletion();
                Debug.Log($"Load settings {instance}");
#if UNITY_EDITOR
                ResetOnPlayModeChange();
#endif
            }
        }

#if UNITY_EDITOR
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

        private static string GetPath()
        {
            var type = typeof(T);
            var attribute = (SettingsPathAttribute)type.GetCustomAttribute(typeof(SettingsPathAttribute));
            var path = attribute != null ? attribute.Path : "Assets/Settings/";
            return Path.Combine(path, ObjectNames.NicifyVariableName(type.Name) + ".asset");
        }
#endif

        private static void CreateDefault()
        {
            var type = typeof(T);
            instance = CreateInstance<T>();
#if UNITY_EDITOR
            var path = GetPath();
            instance.name = $"{ObjectNames.NicifyVariableName(type.Name)}";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(instance, path);
            Debug.Log($"Create default settings:{type.Name}", instance);
            AssetDatabase.SaveAssets();
            SaveAsAddressable();
#endif
        }

#if UNITY_EDITOR
        private static void SaveAsAddressable()
        {
            if (instance == null) return;
            var name = typeof(T).Name;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.AddLabel(name);
            var group = settings.FindGroup(SettingsConst.AddressableGroupName);
            group ??= settings.CreateGroup(SettingsConst.AddressableGroupName, false, true, false, settings.DefaultGroup.Schemas);
            var guid = AssetDatabase.AssetPathToGUID(GetPath());
            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.labels.Add(name);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
            AssetDatabase.SaveAssets();
        }

        protected static SoSettingsProvider<T> GetDefaultSettings(string settingsName)
        {
            return new SoSettingsProvider<T>(Instance, settingsName);
        }
#endif
    }
}