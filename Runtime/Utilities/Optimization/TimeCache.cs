using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Core.Utilities.Optimization
{
    [DefaultExecutionOrder(-3000)]
    public class TimeCache : Singleton<TimeCache>
    {
        [ShowInInspector, ReadOnly] public static float DeltaTime;
        [ShowInInspector, ReadOnly] public static float FixedDeltaTime;
        [ShowInInspector, ReadOnly] public static float Time;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            TryCreateDefault();
        }

        private void Start()
        {
            DeltaTime = UnityEngine.Time.deltaTime;
            FixedDeltaTime = UnityEngine.Time.fixedDeltaTime;
            Time = UnityEngine.Time.time;
        }

        private void OnDestroy()
        {
            Time = 0;
            DeltaTime = 0;
        }

        private void FixedUpdate()
        {
            FixedDeltaTime = UnityEngine.Time.fixedDeltaTime;
        }

        private void Update()
        {
            DeltaTime = UnityEngine.Time.deltaTime;
            Time = UnityEngine.Time.time;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }
}