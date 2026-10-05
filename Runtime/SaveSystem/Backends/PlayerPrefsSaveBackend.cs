using System;
using UnityEngine;

namespace Core.SaveSystem.Backends
{
    /// <summary>
    /// The save as one PlayerPrefs string. Meant for WebGL, where PlayerPrefs is the reliable store; it is
    /// flushed on every write, since PlayerPrefs alone only persists on a clean quit.
    /// </summary>
    public sealed class PlayerPrefsSaveBackend : ISaveBackend
    {
        public string Key { get; }
        private string BackupKey => Key + ".bak";
        private string CorruptKey => Key + ".corrupt";

        public PlayerPrefsSaveBackend(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Save key is empty.", nameof(key));
            Key = key;
        }

        public string Read() => PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;

        public string ReadBackup() => PlayerPrefs.HasKey(BackupKey) ? PlayerPrefs.GetString(BackupKey) : null;

        public void Write(string text)
        {
            if (PlayerPrefs.HasKey(Key)) PlayerPrefs.SetString(BackupKey, PlayerPrefs.GetString(Key));
            PlayerPrefs.SetString(Key, text);
            PlayerPrefs.Save();
        }

        public void QuarantineCorrupt()
        {
            if (!PlayerPrefs.HasKey(Key)) return;
            PlayerPrefs.SetString(CorruptKey, PlayerPrefs.GetString(Key));
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        public void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(BackupKey);
            PlayerPrefs.Save();
        }

        public override string ToString() => $"PlayerPrefs[{Key}]";
    }
}
