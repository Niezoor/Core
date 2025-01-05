using System;
using UnityEngine;

namespace Core.Utilities
{
    public static class VectorExtension
    {
        public static bool Almost(this Vector3 vec1, Vector3 vec2)
        {
            return Mathf.Approximately(vec1.x, vec2.x) &&
                   Mathf.Approximately(vec1.y, vec2.y) &&
                   Mathf.Approximately(vec1.z, vec2.z);
        }

        public static bool Almost(this Vector2 vector2, Vector2 other, float tolerance) =>
            Math.Abs(vector2.x - other.x) < tolerance && Math.Abs(vector2.y - other.y) < tolerance;

        public static Vector2 Direction(this Vector2 from, Vector2 to)
        {
            return to - from;
        }

        public static Vector3 Direction(this Vector3 from, Vector3 to)
        {
            return to - from;
        }

        public static float DistanceSqr(this Vector2 a, Vector2 b)
        {
            float num1 = a.x - b.x;
            float num2 = a.y - b.y;
            return (float)((double)num1 * (double)num1 + (double)num2 * (double)num2);
        }
    }
}