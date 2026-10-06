using Core.Editor.Inspector;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;

namespace Core.UISystem.Editor
{
    /// <summary>
    /// Keeps every screen class in IL2CPP builds: <c>Screens.Push(Type)</c> and <c>StartScreen</c> create them by
    /// type name, which the linker cannot see.
    /// </summary>
    internal sealed class ScreenLinkXml : IUnityLinkerProcessor
    {
        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            var writer = new LinkXmlWriter();
            foreach (var type in ScreenRegistry.ScreenTypes) writer.PreserveType(type);
            return writer.Write(data.inputDirectory, "CoreUISystem.link.xml");
        }
    }
}
