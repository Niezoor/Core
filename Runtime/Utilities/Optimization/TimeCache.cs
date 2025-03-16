using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Core.Utilities.Optimization
{
    [DefaultExecutionOrder(-3000)]
    public class TimeCache : Singleton<TimeCache>
    {
        [ShowInInspector, ReadOnly] public static float deltaTime;
        [ShowInInspector, ReadOnly] public static float fixedDeltaTime;
        [ShowInInspector, ReadOnly] public static float time;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            TryCreateDefault();
        }

        private void Start()
        {
            deltaTime = UnityEngine.Time.deltaTime;
            fixedDeltaTime = UnityEngine.Time.fixedDeltaTime;
            time = UnityEngine.Time.time;
        }

        private void OnDestroy()
        {
            time = 0;
            deltaTime = 0;
        }

        private void FixedUpdate()
        {
            fixedDeltaTime = UnityEngine.Time.fixedDeltaTime;
        }

        private void Update()
        {
            deltaTime = UnityEngine.Time.deltaTime;
            time = UnityEngine.Time.time;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }
}