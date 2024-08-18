using UnityEngine;

namespace Core.Pooling
{
    public class GameObjectPool : PoolInstance<GameObject>
    {
        public GameObjectPool(GameObject prefab, int cap, Transform parent = null) : base(prefab, cap, parent)
        {
        }

        public override GameObject Spawn()
        {
            var instance = GetFromPool();
            instance.SetActive(true);
            return instance;
        }

        public override GameObject Spawn(Transform parent)
        {
            var instance = GetFromPool();
            instance.transform.SetParent(parent, false);
            instance.SetActive(true);
            return instance;
        }

        public override GameObject Spawn(Vector3 position)
        {
            var instance = GetFromPool();
            instance.transform.position = position;
            instance.SetActive(true);
            return instance;
        }

        public override GameObject Spawn(Vector3 position, Quaternion rotation)
        {
            var instance = GetFromPool();
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            return instance;
        }

        public override GameObject Spawn(Vector3 position, Quaternion rotation, Transform parent)
        {
            var instance = GetFromPool();
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            return instance;
        }

        protected override GameObject CreateInstance(bool active)
        {
            var instance = IsParentSet ? Object.Instantiate(Prefab, Parent) : Object.Instantiate(Prefab);
            instance.SetActive(active);
            return instance;
        }

        protected override void Release(GameObject instance)
        {
            if (instance == null) return;
            if (IsParentSet) instance.transform.SetParent(Parent, false);
            instance.SetActive(false);
            PushToPool(instance);
        }

        protected override void Destroy(GameObject instance)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (instance != null) Object.DestroyImmediate(instance);
                return;
            }
#endif
            if (instance != null) Object.Destroy(instance);
        }
    }
}