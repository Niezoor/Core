using System.Reflection;
using Core.Utilities.Inspector;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;

namespace Core.Editor.Inspector
{
    /// <summary>Keeps <c>[Button]</c> methods and <c>[Inspect]</c> members in IL2CPP builds: only reflection calls them.</summary>
    internal sealed class InspectorLinkXml : IUnityLinkerProcessor
    {
        private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                              BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            var writer = new LinkXmlWriter();
            foreach (var method in TypeCache.GetMethodsWithAttribute<ButtonAttribute>()) writer.Preserve(method);
            foreach (var field in TypeCache.GetFieldsWithAttribute<InspectAttribute>()) writer.Preserve(field);

            // TypeCache has no lookup for properties, so the assemblies that can use [Inspect] are scanned.
            foreach (var assembly in LinkXmlWriter.PlayerAssembliesUsing("Core.Utilities"))
            {
                foreach (var type in assembly.GetTypes())
                {
                    foreach (var property in type.GetProperties(Declared))
                    {
                        if (property.IsDefined(typeof(InspectAttribute), false)) writer.Preserve(property);
                    }
                }
            }

            return writer.Write(data.inputDirectory, "CoreInspector.link.xml");
        }
    }
}
