using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
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
        [SerializeField] private bool isAddressable;

        public string ScenePath => scenePath;

        private AsyncOperationHandle<SceneInstance> handle;

        public void LoadScene()
        {
            if (isAddressable)
            {
                Addressables.LoadSceneAsync(ScenePath);
            }
            else
            {
                SceneManager.LoadScene(ScenePath);
            }
        }

        public AsyncOperationHandle<SceneInstance> LoadSceneAsync(LoadSceneMode loadMode,
            SceneReleaseMode releaseMode,
            bool activateOnLoad = true,
            int priority = 100)
        {
            handle = Addressables.LoadSceneAsync(ScenePath, loadMode, releaseMode, activateOnLoad, priority);
            return handle;
        }

        public IEnumerator UnloadSceneAsync()
        {
            if (handle.IsValid())
            {
                yield return Addressables.UnloadSceneAsync(handle).Task;
            }
            else
            {
                yield return SceneManager.UnloadSceneAsync(SceneManager.GetActiveScene());
            }
        }

        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            UpdateData();
#endif
        }

        public void OnAfterDeserialize()
        { }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UpdateData();
        }

        private void UpdateData()
        {
            if (sceneAsset == null) return;
            scenePath = AssetDatabase.GetAssetPath(sceneAsset);
            if (string.IsNullOrEmpty(scenePath)) return;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(scenePath), true);
            isAddressable = entry != null;
        }
#endif
    }
}