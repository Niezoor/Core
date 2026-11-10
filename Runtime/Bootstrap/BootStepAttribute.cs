using System;

namespace Core.Bootstrap
{
    /// <summary>
    /// Puts a <see cref="BootInitializer"/>, <see cref="BootService"/> or <see cref="SplashTask"/> into
    /// <see cref="BootSettings"/> on its own: the editor finds every such type in the player assemblies and adds one
    /// instance of it to its phase (after each compile, before Play and before a build), so a feature brings its boot
    /// steps with it instead of every project listing them by hand. The phase follows from the base class.
    /// </summary>
    /// <remarks>
    /// Removing an added step from the list brings it back on the next sync - switch it off with Enabled instead.
    /// Steps without the attribute can still be added by hand; they sort as <see cref="BootOrder.Default"/>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class BootStepAttribute : Attribute
    {
        /// <summary>Position within the phase, lower first; equal orders keep the order they have in the list.
        /// See <see cref="BootOrder"/>.</summary>
        public int Order { get; set; } = BootOrder.Default;

        /// <summary>False: the step is only offered in the list's dropdown and sorted when added, e.g. one that
        /// needs a value from the game first.</summary>
        public bool AutoAdd { get; set; } = true;
    }

    /// <summary>Common <see cref="BootStepAttribute.Order"/> values, spaced so steps can go between them.</summary>
    public static class BootOrder
    {
        public const int First = -10000;
        /// <summary>Addressables started - before anything loads through them.</summary>
        public const int Addressables = -2000;
        /// <summary>Async settings loaded - before anything that reads them.</summary>
        public const int Settings = -1000;
        /// <summary>The local save loaded.</summary>
        public const int Save = -500;
        /// <summary>The game's own steps, and any step without <see cref="BootStepAttribute"/>.</summary>
        public const int Default = 0;
        public const int Last = 10000;
    }
}
