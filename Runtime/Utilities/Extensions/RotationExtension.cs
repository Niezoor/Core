using UnityEngine;

namespace Core.Utilities.Extensions
{
    public static class RotationExtension
    {
        public static Quaternion ToRotation2D(this Vector2 direction)
        {
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90;
            return Quaternion.AngleAxis(angle, Vector3.forward);
        }

        public static Quaternion ToRotation2D(this Vector3 direction)
        {
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90;
            return Quaternion.AngleAxis(angle, Vector3.forward);
        }

        public static Vector2 RotationToDirection(this Transform transform)
        {
            return transform.rotation * Vector3.forward;
        }

        public static Vector3 ToDirection(this Quaternion rotation, Vector3 axis)
        {
            return rotation * axis;
        }
    }
}