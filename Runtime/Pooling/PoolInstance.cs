using System.Collections.Generic;
using Core.Utilities;
using UnityEngine;

namespace Core.Pooling
{
    public abstract class PoolInstance
    {
        public readonly Transform Parent;
        public readonly bool IsParentSet;
        public readonly bool IsPoolParent;
        public int ActiveCount;
        public int InactiveCount;

        public virtual void Dispose()
        {
            if (!IsPoolParent || !Parent) return;
            if (Application.isPlaying)
            {
                Object.Destroy(Parent.gameObject);
            }
            else
            {
                Object.DestroyImmediate(Parent.gameObject);
            }
        }

        public PoolInstance(Transform parent, string name)
        {
            Parent = parent;
            IsParentSet = parent;
            if (IsParentSet) return;
            var gameObject = new GameObject(name);
            Parent = gameObject.transform;
            Object.DontDestroyOnLoad(gameObject);
            IsParentSet = true;
            IsPoolParent = true;
        }
    }

    public abstract class PoolInstance<T> : PoolInstance where T : Object
    {
        public readonly T Prefab;
        public readonly HashSet<T> Active;
        public readonly List<T> Inactive;

        public PoolInstance(T prefab, int cap, Transform parent = null) : base(parent, prefab.name + " pool")
        {
            this.Prefab = prefab;
            Active = new HashSet<T>(cap);
            Inactive = new List<T>(cap);
        }

        public void Preload(int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                var obj = CreateInstance(false);
                InactiveCount++;
                Inactive.Add(obj);
            }
        }

        public abstract T Spawn();
        public abstract T Spawn(Transform parent);
        public abstract T Spawn(Vector3 position);
        public abstract T Spawn(Vector3 position, Quaternion rotation);
        public abstract T Spawn(Vector3 position, Quaternion rotation, Transform parent);

        public void Despawn(T obj)
        {
            Release(obj);
        }

        public void DespawnAll()
        {
            var currentActive = new List<T>(Active);
            foreach (var poolable in currentActive)
            {
                Despawn(poolable);
            }

            Active.Clear();
        }

        protected abstract T CreateInstance(bool active);
        protected abstract void Release(T instance);

        protected abstract void Destroy(T instance);

        protected T Create()
        {
            var obj = CreateInstance(false);
            ActiveCount++;
            Active.Add(obj);
            return obj;
        }

        protected T GetFromPool()
        {
            T obj;
            if (InactiveCount > 0)
            {
                InactiveCount--;
                obj = Inactive[InactiveCount];
                Inactive.RemoveAt(InactiveCount);
                Active.Add(obj);
                ActiveCount++;
                return obj;
            }

            return Create();
        }

        protected void PushToPool(T instance)
        {
            InactiveCount++;
            Inactive.Add(instance);
            ActiveCount--;
            Active.Remove(instance);
        }

        public override void Dispose()
        {
            foreach (var instance in Active)
            {
                Destroy(instance);
            }

            foreach (var instance in Inactive)
            {
                Destroy(instance);
            }

            Active.Clear();
            Inactive.Clear();
            ActiveCount = 0;
            InactiveCount = 0;
            base.Dispose();
        }
    }
}