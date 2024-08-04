using System;
using System.Globalization;
using Core.Utilities;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Core.Save.Storages
{
    [Serializable]
    public class SaveDictionary : UnitySerializedDictionary<string, string>
    {
    }

    [Serializable]
    public class StorageData
    {
        public string userId;
        public string description;
        [HideInInspector] public long playTimeTicks;
        [HideInInspector] public long modificationDateTicks;
        public SaveDictionary data = new();

        [ShowInInspector] public string PlayTime => TimeSpan.FromTicks(playTimeTicks).ToString();
        [ShowInInspector] public string ModificationDate => new DateTime(modificationDateTicks).ToString(CultureInfo.InvariantCulture);
    }
}