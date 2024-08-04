using UnityEngine;

namespace Core.Pooling
{
    public class ComponentPool : PoolInstance<Component>
    {
        public ComponentPool(Component origin, int cap, Transform parent = null) : base(origin, cap, parent)
        {
        }
        
        public override Component Spawn()
        {
            var Instance = ObjectPool.Get();
            Instance.gameObject.SetActive(true);
            return Instance;
        }

        public override Component Spawn(Transform parent)
        {
            var Instance = ObjectPool.Get();
            Instance.transform.SetParent(parent, false);
            Instance.gameObject.SetActive(true);
            return Instance;
        }

        public override Component Spawn(Vector3 position)
        {
            var Instance = ObjectPool.Get();
            Instance.transform.position = position;
            Instance.gameObject.SetActive(true);
            return Instance;
        }

        public override Component Spawn(Vector3 position, Quaternion rotation)
        {
            var Instance = ObjectPool.Get();
            Instance.transform.SetPositionAndRotation(position, rotation);
            Instance.gameObject.SetActive(true);
            return Instance;
        }

        public override Component Spawn(Vector3 position, Quaternion rotation, Transform parent)
        {
            var Instance = ObjectPool.Get();
            Instance.transform.SetParent(parent, false);
            Instance.transform.SetPositionAndRotation(position, rotation);
            Instance.gameObject.SetActive(true);
            return Instance;
        }

        protected override Component Create()
        {
            var instance = IsParentSet ? Object.Instantiate(Prefab, Parent) : Object.Instantiate(Prefab);
            instance.gameObject.SetActive(false);
            return instance;
        }

        protected override void Get(Component instance)
        {
            Active.Add(instance);
        }

        protected override void Release(Component instance)
        {
            if (instance == null)
            {
                return;
            }

            if (IsParentSet)
            {
                instance.transform.SetParent(Parent, false);
            }

            Active.Remove(instance);
            instance.gameObject.SetActive(false);
        }

        protected override void Destroy(Component instance)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (instance != null)
                {
                    Object.DestroyImmediate(instance.gameObject);
                }

                return;
            }
#endif
            if (instance != null)
            {
                Object.Destroy(instance.gameObject);
            }
        }
    }
}