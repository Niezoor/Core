
using UnityEngine;

namespace Core.Utilities
{
    public static class RotationHelper
    {
        public static Quaternion DirectionToRotation2d(Vector2 direction)
        {
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90;
            return Quaternion.AngleAxis(angle, Vector3.forward);
        }

        public static Vector2 RotationToDirection(this Transform transform)
        {
            return transform.rotation * Vector3.forward;
        }
        
        public static Vector3 ToDirection(this Quaternion rotation, Vector3 direction)
        {
            return rotation * direction;
        }
    }
}