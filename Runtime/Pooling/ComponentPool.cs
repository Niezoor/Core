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
            var instance = GetFromPool();
            instance.gameObject.SetActive(true);
            return instance;
        }

        public override Component Spawn(Transform parent)
        {
            var instance = GetFromPool();
            instance.transform.SetParent(parent, false);
            instance.gameObject.SetActive(true);
            return instance;
        }

        public override Component Spawn(Vector3 position)
        {
            var instance = GetFromPool();
            instance.transform.position = position;
            instance.gameObject.SetActive(true);
            return instance;
        }

        public override Component Spawn(Vector3 position, Quaternion rotation)
        {
            var instance = GetFromPool();
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);
            return instance;
        }

        public override Component Spawn(Vector3 position, Quaternion rotation, Transform parent)
        {
            var instance = GetFromPool();
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);
            return instance;
        }

        protected override Component CreateInstance(bool active)
        {
            var instance = IsParentSet ? Object.Instantiate(Prefab, Parent) : Object.Instantiate(Prefab);
            instance.gameObject.SetActive(active);
            return instance;
        }

        protected override void Release(Component instance)
        {
            if (instance == null) return;
            if (IsParentSet) instance.transform.SetParent(Parent, false);
            instance.gameObject.SetActive(false);
            PushToPool(instance);
        }

        protected override void Destroy(Component instance)
        {
            if (instance == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(instance.gameObject);
                return;
            }
#endif
            Object.Destroy(instance.gameObject);
        }
    }
}