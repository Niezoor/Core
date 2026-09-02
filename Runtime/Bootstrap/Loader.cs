using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Core.Save;
using Core.Utilities;
using Core.Utilities.Settings;
using Gameplay.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
#endif

namespace Bootstrap
{
    public class Loader : MonoBehaviour
    {
        public SceneRef NextScene;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeInEditor()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (!sceneName.Equals("Loader") && !sceneName.Equals("Launcher"))
            {
                Debug.Log($"Initialize game in editor: {sceneName}");
                SettingsManager.Instance.LoadSettings();
            }
        }
#endif

        private IEnumerator Start()
        {
            Debug.Log($"Loading start: {Time.realtimeSinceStartup.ToString(CultureInfo.InvariantCulture)}s");
            yield return new WaitUntil(() => Save.IsLoaded);
            Debug.Log($"Loading save: {Time.realtimeSinceStartup.ToString(CultureInfo.InvariantCulture)}s");
            var locations = Addressables.LoadResourceLocationsAsync(SettingsConst.AddressableGroupName);
            yield return locations;
            var loadOps = new List<AsyncOperationHandle>(locations.Result.Count);
            foreach (var location in locations.Result)
            {
                var handle = Addressables.LoadAssetAsync<ScriptableObject>(location);
                Debug.Log($"Loading settings: {location}");
                loadOps.Add(handle);
            }

            SettingsManager.Instance.LoadSettings();
            yield return Addressables.ResourceManager.CreateGenericGroupOperation(loadOps, true);
            Debug.Log($"Loading settings complete: {Time.realtimeSinceStartup.ToString(CultureInfo.InvariantCulture)}s");
            yield return NextScene.LoadSceneAsync(LoadSceneMode.Single, SceneReleaseMode.ReleaseSceneWhenSceneUnloaded);
            Debug.Log($"Loading next scene: {Time.realtimeSinceStartup.ToString(CultureInfo.InvariantCulture)}s");
        }
    }
}