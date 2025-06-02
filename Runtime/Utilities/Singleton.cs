using UnityEngine;

namespace Core.Utilities
{
    public class Singleton : MonoBehaviour
    {
        public static T1 GetOrCreateDefault<T1>(bool dontDestroy = true, HideFlags flags = HideFlags.None)
            where T1 : MonoBehaviour
        {
            var instance = FindFirstObjectByType<T1>();
            if (instance != null) return instance;
            var obj = new GameObject
            {
                name = $"[{typeof(T1).Name}]",
#if UNITY_EDITOR
                hideFlags = flags
#endif
            };
            Debug.Log($"Creating new instance of {obj.name}");
            instance = obj.AddComponent<T1>();
            if (dontDestroy)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) return instance;
#endif
                DontDestroyOnLoad(obj);
            }

            return instance;
        }
    }

    [DefaultExecutionOrder(-1000)]
    public class Singleton<T> : Singleton where T : MonoBehaviour
    {
        protected static T instance;

        public static bool HasInstance => instance != null;

        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = GetOrCreateDefault<T>();
                }

                return instance;
            }
        }

        protected static void TryCreateDefault()
        {
            if (instance == null)
            {
                instance = GetOrCreateDefault<T>();
            }
        }

        protected virtual void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}