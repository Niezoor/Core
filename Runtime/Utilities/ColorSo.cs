using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Core.Utilities
{
    [CreateAssetMenu(fileName = "Color", menuName = "Core/Utilities/Color SO")]
    public class ColorSo : ScriptableObject
    {
#if UNITY_EDITOR
#endif
        [FormerlySerializedAs("color")] public Color Color = Color.white;
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ColorSo), true)]
    [CanEditMultipleObjects]
    public class ColorSoEditor : Editor
    {
        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
        {
            var texture = new Texture2D(width, height);
            var color = target as ColorSo;
            Color[] pixels = Enumerable.Repeat(color.Color, width * height).ToArray();
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Type GetType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null)
                return type;

            if (typeName.Contains("."))
            {
                var assemblyName = typeName.Substring(0, typeName.IndexOf('.'));
                var assembly = Assembly.Load(assemblyName);
                if (assembly == null)
                    return null;
                type = assembly.GetType(typeName);
                if (type != null)
                    return type;
            }

            var currentAssembly = Assembly.GetExecutingAssembly();
            var referencedAssemblies = currentAssembly.GetReferencedAssemblies();
            foreach (var assemblyName in referencedAssemblies)
            {
                var assembly = Assembly.Load(assemblyName);
                if (assembly != null)
                {
                    type = assembly.GetType(typeName);
                    if (type != null)
                        return type;
                }
            }

            return null;
        }
    }
#endif
}