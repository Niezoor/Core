using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Core.Utilities
{
    [DefaultExecutionOrder(-1000)]
    public abstract class SingletonPrefab<T> : MonoBehaviour where T : Component
    {
        public static T Instance;

        public static bool HasInstance => Instance != null;

        [SerializeField] private bool dontDestroyOnLoad;

        protected void Awake()
        {
            if (Instance == null)
            {
                Instance = this as T;
                SingletonAwake();
                if (dontDestroyOnLoad)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else
            {
                Debug.Log($"Destroying duplicate singleton:{gameObject}", gameObject);
                Destroy(gameObject);
            }
        }

        protected void Start()
        {
            if (Instance == this)
            {
                SingletonStart();
            }
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                SingletonOnDestroy();
            }
        }

        public static void DestroyInstance()
        {
            if (Instance != null)
            {
                Destroy(Instance.gameObject);
                Instance = null;
            }
        }

        protected virtual void SingletonAwake()
        { }

        protected virtual void SingletonStart()
        { }

        protected virtual void SingletonOnDestroy()
        { }

#if UNITY_EDITOR
        [OnInspectorGUI, PropertyOrder(-10000)]
        private void DrawSingletonLabel()
        {
            EditorGUILayout.HelpBox("Singleton", MessageType.None, true);
        }
#endif
    }
}