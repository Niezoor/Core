using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Utilities.Inspector
{
    /// <summary>
    /// What a runtime inspector shows for an object: the fields Unity's inspector shows (public or
    /// <c>[SerializeField]</c>, without <c>[NonSerialized]</c>/<c>[HideInInspector]</c>), members with
    /// <see cref="InspectAttribute"/> and <see cref="ButtonAttribute"/> methods. No UI here; views build on it.
    /// </summary>
    public static class InspectorModel
    {
        private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                              BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // Reflection results never go stale while the domain lives, so unlike game state they are kept across Play sessions.
        private static readonly Dictionary<Type, MemberInfo[]> cache = new();

        /// <summary>Rows for the object, base class members first.</summary>
        public static IReadOnlyList<InspectorMember> GetMembers(object target)
        {
            if (target == null) return Array.Empty<InspectorMember>();
            return GetMembers(target.GetType(), () => target, null, 0);
        }

        internal static List<InspectorMember> GetMembers(Type type, Func<object> owner, Action<object> writeOwner,
            int depth)
        {
            var members = new List<InspectorMember>();
            foreach (var member in GetInspectedMembers(type))
            {
                members.Add(new InspectorMember(member, owner, writeOwner, depth));
            }

            return members;
        }

        /// <summary>Whether a value of this type is shown as members of its own (a nested serializable object).</summary>
        public static bool IsNested(Type type)
        {
            if (type == null || type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
                return false;
            if (typeof(Object).IsAssignableFrom(type) || typeof(Delegate).IsAssignableFrom(type)) return false;
            if (type.Namespace != null && type.Namespace.StartsWith("UnityEngine")) return false;
            if (IsList(type)) return false;
            return type.IsDefined(typeof(SerializableAttribute), false) || GetInspectedMembers(type).Length > 0;
        }

        public static bool IsList(Type type) => type != null && type != typeof(string) && typeof(IList).IsAssignableFrom(type);

        public static Type GetElementType(Type listType)
        {
            if (listType.IsArray) return listType.GetElementType();
            var list = listType.GetInterfaces().Append(listType)
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
            return list?.GetGenericArguments()[0] ?? typeof(object);
        }

        /// <summary>"m_maxHealth", "_maxHealth" and "&lt;MaxHealth&gt;k__BackingField" all become "Max Health".</summary>
        public static string Nicify(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            var start = name.IndexOf('<');
            var end = name.IndexOf('>');
            if (start >= 0 && end > start) name = name.Substring(start + 1, end - start - 1);
            if (name.StartsWith("m_") && name.Length > 2) name = name.Substring(2);
            name = name.TrimStart('_');
            if (name.Length == 0) return string.Empty;

            var builder = new System.Text.StringBuilder(name.Length + 8);
            var capitalize = true;
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (c == '_')
                {
                    if (builder.Length > 0 && builder[builder.Length - 1] != ' ') builder.Append(' ');
                    capitalize = true;
                    continue;
                }

                if (builder.Length > 0 && builder[builder.Length - 1] != ' ')
                {
                    var previous = name[i - 1];
                    var acronymEnd = char.IsUpper(previous) && i + 1 < name.Length && char.IsLower(name[i + 1]);
                    if (char.IsUpper(c) && (char.IsLower(previous) || char.IsDigit(previous) || acronymEnd) ||
                        char.IsDigit(c) && char.IsLetter(previous))
                    {
                        builder.Append(' ');
                    }
                }

                builder.Append(capitalize ? char.ToUpperInvariant(c) : c);
                capitalize = false;
            }

            return builder.ToString();
        }

        private static MemberInfo[] GetInspectedMembers(Type type)
        {
            if (cache.TryGetValue(type, out var members)) return members;

            var chain = new List<Type>();
            for (var t = type; t != null && !IsEngineBase(t); t = t.BaseType) chain.Add(t);
            chain.Reverse();

            var found = new List<MemberInfo>();
            foreach (var t in chain)
            {
                found.AddRange(t.GetFields(Declared).Where(IsInspected).OrderBy(f => f.MetadataToken));
                found.AddRange(t.GetProperties(Declared)
                    .Where(p => p.IsDefined(typeof(InspectAttribute), true) && p.GetIndexParameters().Length == 0)
                    .OrderBy(p => p.MetadataToken));
            }

            found.AddRange(ButtonMethods.Get(type));
            members = found.ToArray();
            cache[type] = members;
            return members;
        }

        private static bool IsEngineBase(Type type) =>
            type == typeof(object) || type == typeof(Object) || type == typeof(ScriptableObject) ||
            type == typeof(Component) || type == typeof(Behaviour) || type == typeof(MonoBehaviour) ||
            type == typeof(ValueType);

        private static bool IsInspected(FieldInfo field)
        {
            if (field.IsDefined(typeof(InspectAttribute), false)) return true;
            if (field.IsStatic || field.IsLiteral || field.IsInitOnly) return false;
            if (field.IsDefined(typeof(NonSerializedAttribute), false) ||
                field.IsDefined(typeof(HideInInspector), false)) return false;
            // An auto-property's backing field counts only when marked [field: SerializeField].
            if (field.IsDefined(typeof(CompilerGeneratedAttribute), false) &&
                !field.IsDefined(typeof(SerializeField), false)) return false;
            return field.IsPublic || field.IsDefined(typeof(SerializeField), false) ||
                   field.IsDefined(typeof(SerializeReference), false);
        }
    }
}
