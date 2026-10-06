using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Core.Display
{
    /// <summary>
    /// The raw screen a <see cref="ScreenState"/> is computed from. Tests and the debug menu pass made-up values
    /// through <see cref="ScreenProfile.Override"/>.
    /// </summary>
    public readonly struct ScreenMetrics : IEquatable<ScreenMetrics>
    {
        /// <param name="density">Pixels per dp; 0 or less means unknown and counts as 1.</param>
        /// <param name="safeArea">In pixels, origin bottom-left like <see cref="Screen.safeArea"/>; empty = whole screen.</param>
        public ScreenMetrics(int width, int height, float density, Rect safeArea, bool isMobile)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            Density = density > 0f ? density : 1f;
            SafeArea = safeArea.width > 0f && safeArea.height > 0f ? safeArea : new Rect(0f, 0f, Width, Height);
            IsMobile = isMobile;
        }

        public int Width { get; }
        public int Height { get; }

        /// <summary>Pixels per dp, the unit the layout thinks in.</summary>
        public float Density { get; }

        public Rect SafeArea { get; }
        public bool IsMobile { get; }

        public static ScreenMetrics Read()
        {
            var width = Screen.width;
            var isMobile = Application.isMobilePlatform;
            return new ScreenMetrics(width, Screen.height, ReadDensity(width, isMobile), Screen.safeArea, isMobile);
        }

        private static float ReadDensity(int width, bool isMobile)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Screen.dpi is unreliable in browsers; the page lays out in CSS pixels, so they serve as dp.
            var cssWidth = CoreDisplay_GetCanvasCssWidth();
            if (cssWidth > 0f) return width / cssWidth;
#endif
            var dpi = Screen.dpi;
            if (dpi <= 0f) return 1f;
            // Each platform's own baseline: Android and iOS lay out in 160 dpi units, desktops in 96 dpi units.
            return dpi / (isMobile ? 160f : 96f);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern float CoreDisplay_GetCanvasCssWidth();
#endif

        public bool Equals(ScreenMetrics other) =>
            Width == other.Width && Height == other.Height && Density.Equals(other.Density) &&
            SafeArea.Equals(other.SafeArea) && IsMobile == other.IsMobile;

        public override bool Equals(object obj) => obj is ScreenMetrics other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Width, Height, Density, SafeArea, IsMobile);

        public override string ToString() =>
            $"{Width}x{Height} @{Density:0.##}x, safe {SafeArea}, {(IsMobile ? "mobile" : "desktop")}";
    }
}
