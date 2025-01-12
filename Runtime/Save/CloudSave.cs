using System;
using System.Collections.Generic;

namespace Core.Save
{
    public abstract class CloudSave
    {
        public abstract event Action<CloudSave> onConflict;
        public abstract event Action<CloudSave> onLoaded;
        public abstract bool IsInitialized { get; set; }
        public abstract bool InitializationInProgress { get; set; }
        public abstract bool IsConflictDetected { get; set; }
        public abstract PlayerSaveData Data { get; set; }
        public abstract List<PlayerSaveData> ConflictData { get; set; }
        public abstract void Sync(Action onFinish);
        public abstract void ResolveConflict(PlayerSaveData chosenData);
    }
}