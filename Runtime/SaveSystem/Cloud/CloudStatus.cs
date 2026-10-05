namespace Core.SaveSystem.Cloud
{
    /// <summary>
    /// Where cloud sync stands, for an icon in the menu. Changes only when a sync finishes, so routine background
    /// syncs don't make it flicker - check <see cref="CloudSave.IsSyncing"/> for a spinner.
    /// </summary>
    public enum CloudStatus
    {
        /// <summary>No provider registered: this build or platform has no cloud.</summary>
        Disabled,
        /// <summary>Registered, but no sync has finished yet this session.</summary>
        Loading,
        /// <summary>The last sync succeeded. Local changes since then go up with the next one.</summary>
        Synced,
        /// <summary>Not signed in or unreachable; retried with back-off. The game plays on the local save.</summary>
        Offline,
        /// <summary>
        /// Reachable but the sync could not complete: the cloud save is unreadable or from a newer game version, an
        /// upload failed, or the local save is read-only. See <see cref="CloudSave.LastError"/>.
        /// </summary>
        Error,
        /// <summary>
        /// The cloud changed since the last sync; nothing moves until the player picks a side through
        /// <see cref="CloudSave.ResolveConflictAsync"/>.
        /// </summary>
        Conflict,
    }
}
