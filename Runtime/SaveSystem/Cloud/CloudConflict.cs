namespace Core.SaveSystem.Cloud
{
    public enum CloudSyncResult
    {
        /// <summary>The cloud already holds the local save.</summary>
        UpToDate,
        Uploaded,
        Downloaded,
        /// <summary>The cloud changed since the last sync; waiting for <see cref="CloudConflictChoice"/>.</summary>
        Conflict,
        /// <summary>Not signed in or unreachable; nothing was touched.</summary>
        Unavailable,
        Failed,
        /// <summary>Not attempted: already syncing, nothing to resolve, or the local save is read-only.</summary>
        Skipped,
    }

    public enum CloudConflictChoice
    {
        KeepLocal,
        KeepCloud,
    }

    /// <summary>The two saves a player has to choose between, with what a prompt needs to tell them apart.</summary>
    public sealed class CloudConflict
    {
        public SaveSnapshot Local { get; }
        public SaveSnapshot Cloud { get; }

        /// <summary>
        /// False when only the cloud moved on since the last sync (the local save is just older); true when both
        /// sides changed, so either choice loses something.
        /// </summary>
        public bool LocalChanged { get; }

        public CloudConflict(SaveSnapshot local, SaveSnapshot cloud, bool localChanged)
        {
            Local = local;
            Cloud = cloud;
            LocalChanged = localChanged;
        }
    }
}
