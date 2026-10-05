using System;
using Core.Bootstrap;
using Core.Utilities.Notifications;

namespace Core.SaveSystem
{
    /// <summary>
    /// Loads the local save before the first scene, and tells the player through <see cref="Notices"/> when it was
    /// damaged or can't be written.
    /// </summary>
    [Serializable]
    public sealed class LoadSaveInitializer : BootInitializer
    {
        public const string ReadOnlyNoticeId = "core.save.read_only";
        public const string RestoredNoticeId = "core.save.restored_backup";
        public const string LostNoticeId = "core.save.lost";

        public override void Initialize()
        {
            // Read the outcome from the state: subscribing to Save.ErrorRaised would itself load the save first.
            Save.Load();
            var corrupt = Save.LastError?.Kind == SaveErrorKind.Corrupt;

            if (Save.IsReadOnly)
            {
                Notices.Post(Notice.Acknowledge(ReadOnlyNoticeId, NoticeSeverity.Error, ReadOnlyNoticeId,
                    "Your progress can't be saved: {0}", new object[] { Save.ReadOnlyReason }));
            }
            else if (corrupt && Save.IsDirty)
            {
                // Restoring the backup marks the save dirty, so it is written back as the main file.
                Notices.Post(Notice.Acknowledge(RestoredNoticeId, NoticeSeverity.Warning, RestoredNoticeId,
                    "Your save was damaged and has been restored from a backup. Some recent progress may be missing."));
            }
            else if (corrupt)
            {
                Notices.Post(Notice.Acknowledge(LostNoticeId, NoticeSeverity.Error, LostNoticeId,
                    "Your save was damaged and could not be restored. The damaged file was kept aside."));
            }
        }
    }
}
