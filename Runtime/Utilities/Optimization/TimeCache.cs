using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Core.Utilities.Optimization
{
    [DefaultExecutionOrder(-3000)]
    public class TimeCache : PersistentSingleton<TimeCache>
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
            deltaTime = Time.deltaTime;
            fixedDeltaTime = Time.fixedDeltaTime;
            time = Time.time;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            time = 0;
            deltaTime = 0;
        }

        private void FixedUpdate()
        {
            fixedDeltaTime = Time.fixedDeltaTime;
        }

        private void Update()
        {
            deltaTime = Time.deltaTime;
            time = Time.time;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }
}