using UnityEngine;
#if UNITY_EDITOR
#endif

namespace Core.Utilities
{
    [DefaultExecutionOrder(-1000)]
    public class PersistentSingleton<T> : Singleton where T : MonoBehaviour
    {
        protected static T instance;

        public static bool HasInstance => instance;

        public static T Instance
        {
            get
            {
                if (!instance)
                {
                    instance = GetOrCreateDefault<T>(true);
                }

                return instance;
            }
        }

        protected static void TryCreateDefault()
        {
            if (!instance)
            {
                instance = GetOrCreateDefault<T>(true);
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