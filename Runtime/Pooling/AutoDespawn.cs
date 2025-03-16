using Core.Utilities.Optimization;
using UnityEngine;

namespace Core.Pooling
{
    public class AutoDespawn : MonoBehaviour
    {
        [SerializeField] private float duration = 1;

        private float currentDuration;

        public float Duration
        {
            get => currentDuration;
            set
            {
                currentDuration = value;
                duration = value;
            }
        }

        private void OnEnable()
        {
            currentDuration = duration;
        }

        private void Update()
        {
            currentDuration -= TimeCache.deltaTime;
            if (currentDuration <= 0)
            {
                gameObject.Despawn();
            }
        }
    }
}