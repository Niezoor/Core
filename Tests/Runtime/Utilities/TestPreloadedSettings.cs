using Core.Utilities.Settings;

namespace Core.Utilities.Tests
{
    // In a file of its own name: Unity ties a ScriptableObject to its script only that way.
    [PreloadedSettings]
    public sealed class TestPreloadedSettings : SettingsAsset<TestPreloadedSettings>
    {
    }
}
