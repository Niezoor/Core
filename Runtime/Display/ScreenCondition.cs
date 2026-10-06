using System;
using UnityEngine;

namespace Core.Display
{
    [Flags]
    public enum SizeClassMask
    {
        Compact = 1 << (int)SizeClass.Compact,
        Medium = 1 << (int)SizeClass.Medium,
        Expanded = 1 << (int)SizeClass.Expanded,
        All = Compact | Medium | Expanded,
    }

    [Flags]
    public enum OrientationMask
    {
        Landscape = 1 << (int)Orientation.Landscape,
        Portrait = 1 << (int)Orientation.Portrait,
        Both = Landscape | Portrait,
    }

    [Flags]
    public enum DeviceMask
    {
        Desktop = 1,
        Mobile = 2,
        Both = Desktop | Mobile,
    }

    /// <summary>
    /// Which screens something is meant for, e.g. a layout variant or an object shown only on phones in portrait.
    /// Every axis must match; the default matches everything.
    /// </summary>
    [Serializable]
    public sealed class ScreenCondition
    {
        [SerializeField] private SizeClassMask sizes = SizeClassMask.All;
        [SerializeField] private OrientationMask orientations = OrientationMask.Both;
        [SerializeField] private DeviceMask devices = DeviceMask.Both;

        public ScreenCondition()
        { }

        public ScreenCondition(SizeClassMask sizes, OrientationMask orientations = OrientationMask.Both,
            DeviceMask devices = DeviceMask.Both)
        {
            this.sizes = sizes;
            this.orientations = orientations;
            this.devices = devices;
        }

        public SizeClassMask Sizes => sizes;
        public OrientationMask Orientations => orientations;
        public DeviceMask Devices => devices;

        public bool Matches(in ScreenState state) =>
            (sizes & (SizeClassMask)(1 << (int)state.SizeClass)) != 0 &&
            (orientations & (OrientationMask)(1 << (int)state.Orientation)) != 0 &&
            (devices & (state.IsMobile ? DeviceMask.Mobile : DeviceMask.Desktop)) != 0;
    }
}
