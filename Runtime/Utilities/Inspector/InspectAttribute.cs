using System;

namespace Core.Utilities.Inspector
{
    /// <summary>
    /// Shows a property or a non-serialized field in the runtime inspector (serialized fields are shown anyway) and
    /// sets how it is shown. One attribute with options instead of several, so it never clashes with Odin's names.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class InspectAttribute : Attribute
    {
        /// <summary>Row label; the member name when empty.</summary>
        public string Label { get; set; }

        public bool ReadOnly { get; set; }

        /// <summary>With <see cref="Max"/>: a slider, like <c>[Range]</c> - which properties can't carry.</summary>
        public float Min { get; set; } = float.NaN;

        public float Max { get; set; } = float.NaN;

        /// <summary>
        /// Name of a parameterless method on the same object, called after the value is changed from the inspector -
        /// a build has no <c>OnValidate</c>.
        /// </summary>
        public string OnChanged { get; set; }
    }
}
