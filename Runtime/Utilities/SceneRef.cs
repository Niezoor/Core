using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
#endif

namespace Core.Utilities
{
    [CreateAssetMenu(fileName = "SceneRef", menuName = "Core/Utilities/SceneRef")]
    public class SceneRef : ScriptableObject, ISerializationCallbackReceiver
    {
#if UNITY_EDITOR
        [SerializeField] private SceneAsset sceneAsset;
#endif
        [SerializeField, ReadOnly] private string scenePath;

        [ShowInInspector] public bool IsAddressable { get; private set; }
        public string ScenePath => scenePath;

        public void LoadScene()
        {
            if (IsAddressable)
            {
                Addressables.LoadSceneAsync(ScenePath);
            }
            else
            {
                SceneManager.LoadScene(ScenePath);
            }
        }

        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            UpdateData();
#endif
        }

        public void OnAfterDeserialize()
        {
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UpdateData();
        }
#endif

        private void UpdateData()
        {
            if (sceneAsset == null) return;
            scenePath = AssetDatabase.GetAssetPath(sceneAsset);
            if (string.IsNullOrEmpty(scenePath)) return;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(scenePath), true);
            IsAddressable = entry != null;
        }

        /*public string ScenePath
        {
            get
            {
#if UNITY_EDITOR
                UpdateReference();
#endif
                return scenePath;
            }
            private set
            {
#if UNITY_EDITOR
                sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(value);
                scenePath = value;
#else
                Debug.LogError($"Setup scene outside of UnityEditor will not work");
#endif
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(sceneAsset);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path), true);
            var isAddressable = entry != null;
            if (IsAddressable != isAddressable)
            {
                IsAddressable = entry != null;
                SetRefDirty();
            }
        }
#endif

        public void LoadScene()
        {
            if (IsAddressable)
            {
                Addressables.LoadSceneAsync(ScenePath);
            }
            else
            {
                SceneManager.LoadScene(ScenePath);
            }
        }

        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            UpdateReference();
#endif
        }

        public void OnAfterDeserialize()
        {
#if UNITY_EDITOR
            EditorApplication.update += HandleAfterDeserialize;
#endif
        }

#if UNITY_EDITOR
        private void HandleAfterDeserialize()
        {
            EditorApplication.update -= HandleAfterDeserialize;
            UpdateReference();
        }

        private void UpdateReference()
        {
            if (sceneAsset == null)
            {
                if (string.IsNullOrEmpty(scenePath))
                {
                    return;
                }

                sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            }
            else
            {
                var path = AssetDatabase.GetAssetPath(sceneAsset);
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                if (!string.Equals(path, scenePath, StringComparison.Ordinal))
                {
                    scenePath = path;
                    SetRefDirty();
                }
            }
        }

        private void SetRefDirty()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        [PostProcessScene]
        private static void OnPostProcessScene()
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(SceneRef)}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<SceneRef>(path);
                if (asset)
                {
                    asset.UpdateReference();
                }
            }
        }
#endif*/
    }
}