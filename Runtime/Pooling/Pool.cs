using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Core.Pooling
{
    public static class Pool
    {
        public static readonly Dictionary<GameObject, GameObjectPool> GameObjectPools = new(64);
        public static readonly Dictionary<Component, ComponentPool> ComponentPools = new(256);

        private static readonly Dictionary<GameObject, ComponentPool> ComponentLinks = new(256);
        private static readonly Dictionary<GameObject, GameObjectPool> GameObjectLinks = new(64);
        private static GameObject gameObject;
        private static Component component;

        public static void Preload(this GameObject prefab, int amount = 0, Transform parent = null)
        {
            if (!GameObjectPools.ContainsKey(prefab))
            {
                AddGameObjectPool(prefab, amount, parent);
            }
        }

        public static GameObject Spawn(this GameObject prefab)
        {
            var gameObjectPool = FindOrAddGameObjectPool(prefab);
            gameObject = gameObjectPool.Spawn();
            GameObjectLinks.TryAdd(gameObject, gameObjectPool);
            return gameObject;
        }

        public static GameObject Spawn(this GameObject prefab, Transform parent)
        {
            var gameObjectPool = FindOrAddGameObjectPool(prefab);
            gameObject = gameObjectPool.Spawn(parent);
            GameObjectLinks.TryAdd(gameObject, gameObjectPool);
            return gameObject;
        }

        public static GameObject Spawn(this GameObject prefab, Vector3 position)
        {
            var gameObjectPool = FindOrAddGameObjectPool(prefab);
            gameObject = gameObjectPool.Spawn(position);
            GameObjectLinks.TryAdd(gameObject, gameObjectPool);
            return gameObject;
        }

        public static GameObject Spawn<T>(this GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var gameObjectPool = FindOrAddGameObjectPool(prefab);
            gameObject = gameObjectPool.Spawn(position, rotation);
            GameObjectLinks.TryAdd(gameObject, gameObjectPool);
            return gameObject;
        }

        public static GameObject Spawn(this GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            var gameObjectPool = FindOrAddGameObjectPool(prefab);
            gameObject = gameObjectPool.Spawn(position, rotation, parent);
            GameObjectLinks.TryAdd(gameObject, gameObjectPool);
            return gameObject;
        }

        public static void Despawn(this GameObject instance)
        {
            if (GameObjectLinks.TryGetValue(instance, out var gameObjectPool))
            {
                //GameObjectLinks.Remove(instance);
                gameObjectPool.Despawn(instance);
            }
            else
            {
                //Object.Destroy(instance);
                instance.SetActive(false);
            }
        }

        public static void Dispose(this GameObject prefab)
        {
            if (GameObjectPools.TryGetValue(prefab, out var gameObjectPool))
            {
                foreach (var gameObject in gameObjectPool.Active)
                {
                    GameObjectLinks.Remove(gameObject);
                }

                gameObjectPool.Dispose();
                GameObjectPools.Remove(prefab);
            }
        }

        public static void Preload<T>(this T prefab, int amount = 0, Transform parent = null) where T : Component
        {
            if (!ComponentPools.ContainsKey(prefab))
            {
                AddMonoPool(prefab, amount, parent);
            }
        }

        public static T Spawn<T>(this T prefab) where T : Component
        {
            var componentPool = FindOrAddMonoPool(prefab);
            component = componentPool.Spawn();
            ComponentLinks.TryAdd(component.gameObject, componentPool);
            return (T)component;
        }

        public static T Spawn<T>(this T prefab, Transform parent) where T : Component
        {
            var componentPool = FindOrAddMonoPool(prefab);
            component = componentPool.Spawn(parent);
            ComponentLinks.TryAdd(component.gameObject, componentPool);
            return (T)component;
        }

        public static T Spawn<T>(this T prefab, Vector3 position) where T : Component
        {
            var componentPool = FindOrAddMonoPool(prefab);
            component = componentPool.Spawn(position);
            ComponentLinks.TryAdd(component.gameObject, componentPool);
            return (T)component;
        }

        public static T Spawn<T>(this T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            var componentPool = FindOrAddMonoPool(prefab);
            component = componentPool.Spawn(position, rotation);
            ComponentLinks.TryAdd(component.gameObject, componentPool);
            return (T)component;
        }

        public static T Spawn<T>(this T prefab, Vector3 position, Quaternion rotation, Transform parent)
            where T : Component
        {
            var componentPool = FindOrAddMonoPool(prefab);
            component = componentPool.Spawn(position, rotation, parent);
            ComponentLinks.TryAdd(component.gameObject, componentPool);
            return (T)component;
        }

        public static T SpawnFromInstance<T>(this T instance, Vector3 position, Quaternion rotation)
            where T : Component
        {
            if (ComponentLinks.TryGetValue(instance.gameObject, out var componentPool))
            {
                //ComponentLinks.Remove(instace.gameObject);
                component = componentPool.Spawn(position, rotation);
                ComponentLinks.TryAdd(component.gameObject, componentPool);
                return (T)component;
            }

            Debug.LogError($"Instance prefab not added to Pool system");
            return null;
        }

        public static void Despawn<T>(this T instance) where T : Component
        {
            if (ComponentLinks.TryGetValue(instance.gameObject, out var componentPool))
            {
                //ComponentLinks.Remove(instance.gameObject);
                componentPool.Despawn(instance);
            }
            else
            {
                //Object.Destroy(instance.gameObject);
                instance.gameObject.SetActive(false);
            }
        }

        public static void DespawnAll<T>(this T prefab) where T : Component
        {
            if (ComponentPools.TryGetValue(prefab, out var componentPool))
            {
                componentPool.DespawnAll();
            }
        }

        public static void Dispose<T>(this T prefab) where T : Component
        {
            if (ComponentPools.TryGetValue(prefab, out var componentPool))
            {
                foreach (var monoBehaviour in componentPool.Active)
                {
                    ComponentLinks.Remove(monoBehaviour.gameObject);
                }

                componentPool.Dispose();
                ComponentPools.Remove(prefab);
            }
        }

        private static GameObjectPool FindOrAddGameObjectPool(GameObject prefab)
        {
            return GameObjectPools.TryGetValue(prefab, out var gameObjectPool)
                ? gameObjectPool
                : AddGameObjectPool(prefab, 0, null);
        }

        private static GameObjectPool AddGameObjectPool(GameObject prefab, int preload, Transform parent)
        {
            var gameObjectPool = new GameObjectPool(prefab, Mathf.Min(preload, 10), parent);
            GameObjectPools.Add(prefab, gameObjectPool);
            if (preload > 0)
            {
                gameObjectPool.Preload(preload);
            }

            return gameObjectPool;
        }

        private static ComponentPool FindOrAddMonoPool(Component prefab)
        {
            return ComponentPools.TryGetValue(prefab, out var componentPool)
                ? componentPool
                : AddMonoPool(prefab, 0, null);
        }

        private static ComponentPool AddMonoPool(Component prefab, int preload, Transform parent)
        {
            var componentPool = new ComponentPool(prefab, Mathf.Min(preload, 10), parent);
            ComponentPools.Add(prefab, componentPool);
            if (preload > 0)
            {
                componentPool.Preload(preload);
            }

            return componentPool;
        }

        static Pool()
        {
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += EditorApplicationOnplayModeStateChanged;
            EditorSceneManager.sceneClosing += DisposeAllPools;
#endif
            SceneManager.sceneUnloaded += s => DisposeAllPools(s, false);
        }

        private static void DisposeAllPools(Scene scene, bool removingScene)
        {
            DisposeAll();
        }

#if UNITY_EDITOR
        private static void EditorApplicationOnplayModeStateChanged(PlayModeStateChange obPlayModeStateChange)
        {
            if (obPlayModeStateChange == PlayModeStateChange.ExitingPlayMode)
            {
                DisposeAll();
            }
        }
#endif

#if UNITY_EDITOR
        [MenuItem("Tools/Pool/Dispose all pools")]
#endif
        public static void DisposeAll()
        {
            foreach (var keyValuePair in GameObjectPools)
            {
                keyValuePair.Value.Dispose();
            }

            foreach (var keyValuePair in ComponentPools)
            {
                keyValuePair.Value.Dispose();
            }

            GameObjectPools.Clear();
            GameObjectLinks.Clear();
            ComponentPools.Clear();
            ComponentLinks.Clear();
            Debug.Log($"All pooled objects cleared");
        }
    }
}