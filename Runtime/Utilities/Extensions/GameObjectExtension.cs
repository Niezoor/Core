using UnityEngine;

namespace Core.Utilities.Extensions
{
    public static class GameObjectExtension
    {
        public static T GetOrAddComponent<T>(this GameObject obj) where T : Component
        {
            var t = obj.GetComponent<T>();

            if (t == null)
            {
                t = obj.AddComponent<T>();
            }

            return t;
        }
    }
}