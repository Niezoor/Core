using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Core.Pooling
{
    public abstract class PoolInstance<T> where T : Object
    {
        public readonly T Prefab;
        public readonly HashSet<T> Active;
        public readonly ObjectPool<T> ObjectPool;
        public readonly Transform Parent;
        public readonly bool IsParentSet;

        public PoolInstance(T prefab, int cap, Transform parent = null)
        {
            this.Prefab = prefab;
            Parent = parent;
            IsParentSet = parent != null;
#if UNITY_EDITOR
            if (!IsParentSet)
            {
                var gameObject = new GameObject(prefab.name + " pool");
                Parent = gameObject.transform;
                IsParentSet = true;
            }
#endif
            Active = new(cap);
            ObjectPool = new ObjectPool<T>(Create, Get, Release, Destroy, false, cap);
        }

        public void Preload(int amount)
        {
            var instances = new List<T>(amount);
            for (int i = 0; i < amount; i++)
            {
                instances.Add(ObjectPool.Get());
            }

            foreach (var instance in instances)
            {
                ObjectPool.Release(instance);
            }
        }

        public abstract T Spawn();
        public abstract T Spawn(Transform parent);
        public abstract T Spawn(Vector3 position);
        public abstract T Spawn(Vector3 position, Quaternion rotation);
        public abstract T Spawn(Vector3 position, Quaternion rotation, Transform parent);

        public void Despawn(T obj)
        {
            ObjectPool.Release(obj);
        }

        public void DespawnAll()
        {
            var currentActive = new HashSet<T>(Active);
            foreach (var poolable in currentActive)
            {
                Despawn(poolable);
            }

            Active.Clear();
        }

        protected abstract T Create();
        protected abstract void Get(T instance);
        protected abstract void Release(T instance);
        protected abstract void Destroy(T instance);

        public void Dispose()
        {
            var currentActive = new HashSet<T>(Active);
            foreach (var poolable in currentActive)
            {
                Despawn(poolable);
            }

            ObjectPool.Dispose();
        }
    }
}