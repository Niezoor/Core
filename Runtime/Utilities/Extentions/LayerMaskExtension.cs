using System.Runtime.CompilerServices;
using UnityEngine;

namespace Core.Utilities
{
    public static class LayerMaskExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this LayerMask mask, int layer)
        {
            return (mask & (1 << layer)) != 0;
        }
    }
}