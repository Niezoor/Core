using System;

namespace Core.Utilities.Settings
{
    /// <summary>
    /// The <see cref="SettingsAsset"/> is in memory from launch (Player Settings &gt; Preloaded Assets), so code running
    /// before the first scene can read it. Everything it references loads with it and delays the start - keep it to
    /// small settings needed that early; the rest load asynchronously.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PreloadedSettingsAttribute : Attribute
    {
    }

    /// <summary>Shows the <see cref="SettingsAsset"/> in Project Settings under this path, e.g. "Game/Gameplay".</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SettingsMenuAttribute : Attribute
    {
        public string Path { get; }

        public SettingsMenuAttribute(string path)
        {
            Path = path;
        }
    }
}
