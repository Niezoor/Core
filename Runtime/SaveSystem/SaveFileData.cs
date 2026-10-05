using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.SaveSystem
{
    [Serializable]
    internal sealed class SaveFileData
    {
        // Always written as at least 1, so 0 after parsing means the text was not a save.
        public int FormatVersion;
        public string SnapshotId;
        public long Revision;
        public string DeviceName;
        public long PlayTimeTicks;
        public long ModifiedUtcTicks;
        public string Description;
        // Local bookkeeping: the snapshot the cloud was last known to hold. Never part of an exported snapshot.
        public string SyncedSnapshotId;
        public List<SaveEntry> Entries = new();

        public static bool TryParse(string text, out SaveFileData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            try
            {
                data = JsonUtility.FromJson<SaveFileData>(text);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return data != null && data.FormatVersion > 0;
        }
    }

    [Serializable]
    internal sealed class SaveEntry
    {
        public string Key;
        public int Version;
        public string Json;
    }
}
