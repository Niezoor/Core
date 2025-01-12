using System;
using System.Globalization;
using Core.Utilities;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Core.Save
{
    [Serializable]
    public class SerializedDictionary : UnitySerializedDictionary<string, string>
    { }

    [Serializable]
    public class PlayerSaveData
    {
        public string UserId;
        public string Description;
        [HideInInspector] public long PlayTimeTicks;
        [HideInInspector] public long ModificationDateTicks;
        public SerializedDictionary Data = new();

#if UNITY_EDITOR
        [OnInspectorGUI]
        private void ShowPlaytimeAndData()
        {
            var playTime = TimeSpan.FromTicks(PlayTimeTicks).ToString();
            var modificationDate = new DateTime(ModificationDateTicks).ToString(CultureInfo.InvariantCulture);
            EditorGUILayout.LabelField($"Playtime: {playTime}");
            EditorGUILayout.LabelField($"Modification date: {modificationDate}");
        }
#endif
    }
}