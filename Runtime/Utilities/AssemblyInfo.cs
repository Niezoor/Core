using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Core.Utilities.Tests")]
[assembly: InternalsVisibleTo("Core.SaveSystem.Tests")]
// SettingsRegistry: Boot marks its initializers, the editor plugs in the asset lookup.
[assembly: InternalsVisibleTo("Core.Bootstrap")]
[assembly: InternalsVisibleTo("Core.Editor")]
