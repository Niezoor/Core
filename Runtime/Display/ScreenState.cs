using System;
using UnityEngine;

namespace Core.Display
{
    /// <summary>How much room the layout has, by the shorter side in dp; the same in both orientations.</summary>
    public enum SizeClass
    {
        /// <summary>Phones, small windows.</summary>
        Compact,

        /// <summary>Tablets, small laptops.</summary>
        Medium,

        /// <summary>Desktop monitors, large tablets.</summary>
        Expanded,
    }

    public enum Orientation
    {
        Landscape,
        Portrait,
    }

    /// <summary>The shorter-side dp at which <see cref="SizeClass.Medium"/> and <see cref="SizeClass.Expanded"/> start.</summary>
    public readonly struct SizeClassThresholds : IEquatable<SizeClassThresholds>
    {
        public static readonly SizeClassThresholds Default = new(600f, 900f);

        public SizeClassThresholds(float mediumMinDp, float expandedMinDp)
        {
            MediumMinDp = mediumMinDp;
            ExpandedMinDp = Mathf.Max(mediumMinDp, expandedMinDp);
        }

        public float MediumMinDp { get; }
        public float ExpandedMinDp { get; }

        public SizeClass Classify(float shortSideDp) =>
            shortSideDp >= ExpandedMinDp ? SizeClass.Expanded
            : shortSideDp >= MediumMinDp ? SizeClass.Medium
            : SizeClass.Compact;

        public bool Equals(SizeClassThresholds other) =>
            MediumMinDp.Equals(other.MediumMinDp) && ExpandedMinDp.Equals(other.ExpandedMinDp);

        public override bool Equals(object obj) => obj is SizeClassThresholds other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(MediumMinDp, ExpandedMinDp);
    }

    /// <summary>Distances from the screen edges to the safe area, in pixels.</summary>
    public readonly struct ScreenInsets : IEquatable<ScreenInsets>
    {
        public ScreenInsets(float left, float right, float top, float bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }

        public float Left { get; }
        public float Right { get; }
        public float Top { get; }
        public float Bottom { get; }

        public ScreenInsets Scaled(float factor) => new(Left * factor, Right * factor, Top * factor, Bottom * factor);

        public bool Equals(ScreenInsets other) =>
            Left.Equals(other.Left) && Right.Equals(other.Right) && Top.Equals(other.Top) && Bottom.Equals(other.Bottom);

        public override bool Equals(object obj) => obj is ScreenInsets other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Left, Right, Top, Bottom);

        public override string ToString() => $"L{Left} R{Right} T{Top} B{Bottom}";
    }

    /// <summary>
    /// The screen as the layout sees it. Sizes in dp already include <see cref="UserScale"/>: a bigger UI leaves less
    /// room, so it can push the layout into a smaller <see cref="SizeClass"/>.
    /// </summary>
    public readonly struct ScreenState : IEquatable<ScreenState>
    {
        internal ScreenState(in ScreenMetrics metrics, SizeClassThresholds thresholds, float userScale)
        {
            Metrics = metrics;
            Thresholds = thresholds;
            UserScale = userScale;
            Scale = metrics.Density * userScale;
            WidthDp = metrics.Width / Scale;
            HeightDp = metrics.Height / Scale;
            ShortSideDp = Mathf.Min(WidthDp, HeightDp);
            SizeClass = thresholds.Classify(ShortSideDp);
            Orientation = metrics.Height > metrics.Width ? Orientation.Portrait : Orientation.Landscape;

            var safe = metrics.SafeArea;
            SafeInsets = new ScreenInsets(
                Mathf.Max(0f, safe.xMin),
                Mathf.Max(0f, metrics.Width - safe.xMax),
                Mathf.Max(0f, metrics.Height - safe.yMax),
                Mathf.Max(0f, safe.yMin));
        }

        public ScreenMetrics Metrics { get; }
        public SizeClassThresholds Thresholds { get; }

        public SizeClass SizeClass { get; }
        public Orientation Orientation { get; }
        public bool IsMobile => Metrics.IsMobile;

        public int Width => Metrics.Width;
        public int Height => Metrics.Height;
        public float Aspect => (float)Metrics.Width / Metrics.Height;

        public float Density => Metrics.Density;
        public float UserScale { get; }

        /// <summary>Pixels per UI unit: <see cref="Density"/> × <see cref="UserScale"/>. Scale the UI by this.</summary>
        public float Scale { get; }

        public float WidthDp { get; }
        public float HeightDp { get; }
        public float ShortSideDp { get; }

        /// <summary>In pixels, origin bottom-left like <see cref="Screen.safeArea"/>.</summary>
        public Rect SafeArea => Metrics.SafeArea;

        /// <summary>In pixels; <c>SafeInsets.Scaled(1f / Scale)</c> gives UI units.</summary>
        public ScreenInsets SafeInsets { get; }

        /// <summary>Same size class, orientation and device kind, i.e. the same layout variant.</summary>
        public bool SameClass(in ScreenState other) =>
            SizeClass == other.SizeClass && Orientation == other.Orientation && IsMobile == other.IsMobile;

        public bool Equals(ScreenState other) =>
            Metrics.Equals(other.Metrics) && Thresholds.Equals(other.Thresholds) && UserScale.Equals(other.UserScale);

        public override bool Equals(object obj) => obj is ScreenState other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Metrics, Thresholds, UserScale);

        public override string ToString() =>
            $"{SizeClass} {Orientation} {(IsMobile ? "mobile" : "desktop")}, {Width}x{Height} px = " +
            $"{WidthDp:0}x{HeightDp:0} dp (scale {Scale:0.##}), safe insets {SafeInsets}";
    }
}
