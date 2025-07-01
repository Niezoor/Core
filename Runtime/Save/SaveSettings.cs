using System;
using Core.Utilities;
using Core.Utilities.Settings;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.Save
{
    [Serializable]
    public enum StorageType
    {
        FileStorage = 0,
        PlayerPrefsStorage = 1,
    }

    public class SaveSettings : SettingsSo<SaveSettings>
    {
        public PlatformSpecific<StorageType> LocalStorageType;

#if UNITY_EDITOR
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider() => GetDefaultSettings("Save");
#endif
    }
}