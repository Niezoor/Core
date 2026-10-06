using System;

namespace Core.Utilities.Inspector
{
    public enum ButtonMode
    {
        Always,
        PlayMode,
        EditMode,
    }

    /// <summary>
    /// Shows the method as a button in the inspector; parameters get fields. Works for private, static and inherited
    /// methods. In its own namespace, because files that import both <c>Core.Utilities</c> and Odin would otherwise
    /// see two <c>[Button]</c> attributes; with Odin installed it is drawn as Odin's button.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public sealed class ButtonAttribute : Attribute
    {
        public ButtonAttribute(string name = null)
        {
            Name = name;
        }

        /// <summary>Button label; the method name when empty.</summary>
        public string Name { get; }

        /// <summary>When the button is enabled.</summary>
        public ButtonMode Mode { get; set; }
    }
}
