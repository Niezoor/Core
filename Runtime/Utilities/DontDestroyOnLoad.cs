using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Core.Utilities
{
    public class DontDestroyOnLoad : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(this);
        }

#if UNITY_EDITOR
        [OnInspectorGUI]
        private void ShowParentlessWarning()
        {
            if (transform.parent != null)
            {
                EditorGUILayout.HelpBox(
                    "DontDestroyOnLoad only works for root GameObjects or components on root GameObjects",
                    MessageType.Error);
            }
        }
#endif
    }
}