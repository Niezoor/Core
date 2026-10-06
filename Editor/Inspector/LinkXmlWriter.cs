using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;

namespace Core.Editor.Inspector
{
    /// <summary>
    /// Writes a link.xml keeping members that only reflection reaches (inspector buttons, screens opened by type name),
    /// which IL2CPP's managed stripping would otherwise remove.
    /// </summary>
    public sealed class LinkXmlWriter
    {
        private readonly SortedDictionary<string, SortedDictionary<string, SortedSet<string>>> assemblies = new();

        public void PreserveType(Type type) => GetType(type).Add("*");

        public void Preserve(MemberInfo member)
        {
            var type = member.DeclaringType;
            if (type == null) return;
            var name = Escape(member.Name);
            GetType(type).Add(member switch
            {
                MethodInfo => $"<method name=\"{name}\" />",
                FieldInfo => $"<field name=\"{name}\" />",
                PropertyInfo => $"<property name=\"{name}\" />",
                _ => throw new ArgumentException($"Cannot preserve {member.MemberType} {member.Name}"),
            });
        }

        public bool IsEmpty => assemblies.Count == 0;

        /// <returns>The written path.</returns>
        public string Write(string directory, string fileName)
        {
            var xml = new StringBuilder("<linker>\n");
            foreach (var (assembly, types) in assemblies)
            {
                // Editor-only and test assemblies are not in the build; the linker must not fail on them.
                xml.Append($"  <assembly fullname=\"{Escape(assembly)}\" ignoreIfMissing=\"1\">\n");
                foreach (var (type, members) in types)
                {
                    if (members.Contains("*"))
                    {
                        xml.Append($"    <type fullname=\"{Escape(type)}\" preserve=\"all\" />\n");
                        continue;
                    }

                    xml.Append($"    <type fullname=\"{Escape(type)}\" preserve=\"nothing\">\n");
                    foreach (var member in members) xml.Append("      ").Append(member).Append('\n');
                    xml.Append("    </type>\n");
                }

                xml.Append("  </assembly>\n");
            }

            xml.Append("</linker>\n");
            var path = Path.Combine(directory, fileName);
            File.WriteAllText(path, xml.ToString());
            return path;
        }

        /// <summary>Loaded assemblies the player build has that reference the given one (and that one itself).</summary>
        public static IEnumerable<Assembly> PlayerAssembliesUsing(string assemblyName)
        {
            var player = new HashSet<string>(UnityEditor.Compilation.CompilationPipeline
                .GetAssemblies(UnityEditor.Compilation.AssembliesType.PlayerWithoutTestAssemblies)
                .Where(a => a.name == assemblyName || a.assemblyReferences.Any(r => r.name == assemblyName))
                .Select(a => a.name));
            return AppDomain.CurrentDomain.GetAssemblies().Where(a => player.Contains(a.GetName().Name));
        }

        private SortedSet<string> GetType(Type type)
        {
            var assembly = type.Assembly.GetName().Name;
            if (!assemblies.TryGetValue(assembly, out var types)) assemblies[assembly] = types = new SortedDictionary<string, SortedSet<string>>();
            // link.xml writes nested types with '/', reflection with '+'.
            var name = (type.IsGenericType ? type.GetGenericTypeDefinition() : type).FullName?.Replace('+', '/') ?? type.Name;
            if (!types.TryGetValue(name, out var members)) types[name] = members = new SortedSet<string>();
            return members;
        }

        private static string Escape(string text) => SecurityElement.Escape(text);
    }
}
