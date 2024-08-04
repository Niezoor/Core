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
            var Instance = ObjectPool.Get();
            Instance.SetActive(true);
            return Instance;
        }

        public override GameObject Spawn(Transform parent)
        {
            var Instance = ObjectPool.Get();
            Instance.transform.SetParent(parent, false);
            Instance.SetActive(true);
            return Instance;
        }

        public override GameObject Spawn(Vector3 position)
        {
            var Instance = ObjectPool.Get();
            Instance.transform.position = position;
            Instance.SetActive(true);
            return Instance;
        }

        public override GameObject Spawn(Vector3 position, Quaternion rotation)
        {
            var Instance = ObjectPool.Get();
            Instance.transform.SetPositionAndRotation(position, rotation);
            Instance.SetActive(true);
            return Instance;
        }

        public override GameObject Spawn(Vector3 position, Quaternion rotation, Transform parent)
        {
           var Instance = ObjectPool.Get();
            Instance.transform.SetParent(parent, false);
            Instance.transform.SetPositionAndRotation(position, rotation);
            Instance.SetActive(true);
            return Instance;
        }

        protected override GameObject Create()
        {
            var instance = IsParentSet ? Object.Instantiate(Prefab, Parent) : Object.Instantiate(Prefab);
            instance.SetActive(false);
            return instance;
        }

        protected override void Get(GameObject instance)
        {
            Active.Add(instance);
        }

        protected override void Release(GameObject instance)
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
            instance.SetActive(false);
        }

        protected override void Destroy(GameObject instance)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }

                return;
            }
#endif
            if (instance != null)
            {
                Object.Destroy(instance);
            }
        }
    }
}