using System.Collections;
using Core.Utilities;
using DG.Tweening;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Bootstrap
{
    public class Launcher : MonoBehaviour
    {
        public SceneRef NextScene;

        private IEnumerator Start()
        {
            yield return null;
            yield return NextScene.LoadSceneAsync(LoadSceneMode.Single, SceneReleaseMode.ReleaseSceneWhenSceneUnloaded);
        }

        public static void Restart()
        {
            DOTween.KillAll();
            SceneManager.LoadScene(0);
        }
    }
}