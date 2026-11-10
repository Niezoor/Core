using System;

namespace Core.Bootstrap
{
    /// <summary>
    /// Makes a <see cref="BootInitializer"/>, <see cref="BootService"/> or <see cref="SplashTask"/> part of the boot:
    /// the editor finds every such type in the player assemblies and writes the steps into <see cref="BootSettings"/>
    /// (after each compile, before Play and before a build), where nobody edits them - a feature brings its boot
    /// steps with it and configures them here, in code. The phase follows from the base class.
    /// </summary>
    /// <remarks>
    /// A step type without the attribute does not run. One that should not run in some project is the feature's
    /// call, through its own settings (the step returns early).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class BootStepAttribute : Attribute
    {
        /// <summary>Position within the phase, lower first; equal orders go by full type name. See
        /// <see cref="BootOrder"/>.</summary>
        public int Order { get; set; } = BootOrder.Default;

        /// <summary>Where the step runs.</summary>
        public BootPlatforms Platforms { get; set; } = BootPlatforms.All;

        /// <summary><see cref="SplashTask"/> only: a failure stops the splash with Retry; otherwise it is logged and
        /// the splash goes on.</summary>
        public bool Required { get; set; } = true;

        /// <summary><see cref="SplashTask"/> only: seconds, 0 = none. Only a task that awaits can be stopped.</summary>
        public float Timeout { get; set; }
    }

    /// <summary>Common <see cref="BootStepAttribute.Order"/> values, spaced so steps can go between them.</summary>
    public static class BootOrder
    {
        public const int First = -10000;
        /// <summary>Addressables started - before anything loads through them.</summary>
        public const int Addressables = -2000;
        /// <summary>Settings loaded - before anything that reads them.</summary>
        public const int Settings = -1000;
        /// <summary>The local save loaded.</summary>
        public const int Save = -500;
        /// <summary>The game's own steps.</summary>
        public const int Default = 0;
        public const int Last = 10000;
    }
}
