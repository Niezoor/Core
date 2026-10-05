using System;

namespace Core.SaveSystem
{
    /// <summary>
    /// A whole save as it travels to and from the cloud: the text to store plus what a conflict prompt shows.
    /// <see cref="SnapshotId"/> is its identity - it changes with every write that changes content, so two
    /// snapshots with the same id hold the same entries.
    /// </summary>
    public sealed class SaveSnapshot
    {
        public string Text { get; }
        public string SnapshotId => data.SnapshotId;
        public long Revision => data.Revision;
        public int FormatVersion => data.FormatVersion;
        public string DeviceName => data.DeviceName;
        public string Description => data.Description;
        public double PlayTime => data.PlayTimeTicks / (double)TimeSpan.TicksPerSecond;
        public DateTime ModifiedUtc => new(data.ModifiedUtcTicks, DateTimeKind.Utc);

        private readonly SaveFileData data;

        internal SaveSnapshot(string text, SaveFileData data)
        {
            Text = text;
            this.data = data;
        }

        /// <summary>False for anything that is not a save written by this system with a snapshot id.</summary>
        public static bool TryParse(string text, out SaveSnapshot snapshot)
        {
            snapshot = null;
            if (!SaveFileData.TryParse(text, out var data) || string.IsNullOrEmpty(data.SnapshotId)) return false;
            snapshot = new SaveSnapshot(text, data);
            return true;
        }

        /// <summary>A fresh copy, so the store never shares objects with a snapshot someone else still holds.</summary>
        internal SaveFileData CopyData()
        {
            SaveFileData.TryParse(Text, out var copy);
            copy.SyncedSnapshotId = null;
            return copy;
        }

        public override string ToString() => $"{SnapshotId} r{Revision} ({DeviceName}, {ModifiedUtc:u})";
    }
}
