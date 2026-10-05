using System;
using Core.Utilities;
using Core.Utilities.Settings;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.Save
{
    [Serializable]
    [System.Obsolete("Core.Save is replaced by Core.SaveSystem (Save, SaveStore). Kept only for existing games.")]
    public enum StorageType
    {
        FileStorage = 0,
        PlayerPrefsStorage = 1,
    }

    [System.Obsolete("Core.Save is replaced by Core.SaveSystem (Save, SaveStore). Kept only for existing games.")]
    public class SaveSettings : ScriptableObjectPreloadedSettings<SaveSettings>
    {
        public PlatformSpecific<StorageType> LocalStorageType;

#if UNITY_EDITOR
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider() => GetDefaultSettings("Core/Save");
#endif
    }
}