using Core.Utilities.Optimization;
using UnityEngine;

namespace Core.Utilities
{
    public class Rotator : MonoBehaviour
    {
        public Vector3 Speed;

        private Transform t;

        private void Awake()
        {
            t = transform;
        }

        private void Update()
        {
            t.Rotate(Speed * TimeCache.DeltaTime);
        }
    }
}