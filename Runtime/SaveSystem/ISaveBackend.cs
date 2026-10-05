namespace Core.SaveSystem
{
    /// <summary>
    /// Where the save text lives. Knows nothing about its contents - parsing, validation and recovery are
    /// <see cref="SaveStore"/>'s job, so every backend gets them for free. Methods may throw; the store handles it.
    /// </summary>
    public interface ISaveBackend
    {
        /// <returns>The save, or null when there is none.</returns>
        string Read();

        /// <returns>The save as it was before the last write, or null when there is none.</returns>
        string ReadBackup();

        /// <summary>
        /// Must be atomic: after a crash at any point either the old or the new text is readable. The previous
        /// save becomes the backup.
        /// </summary>
        void Write(string text);

        /// <summary>Moves an unreadable save out of the way without destroying it.</summary>
        void QuarantineCorrupt();

        /// <summary>Removes the save and its backup.</summary>
        void Delete();
    }
}
