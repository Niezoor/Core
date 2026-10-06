using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Utilities.Extensions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Utilities.Inspector
{
    /// <summary>Finds and calls <see cref="ButtonAttribute"/> methods; shared by the editor and runtime inspectors.</summary>
    public static class ButtonMethods
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                           BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // Reflection results never go stale while the domain lives, so unlike game state they are kept across Play sessions.
        private static readonly Dictionary<Type, MethodInfo[]> cache = new();

        /// <summary>Button methods of the type, base classes first, each in declaration order.</summary>
        public static IReadOnlyList<MethodInfo> Get(Type type)
        {
            if (cache.TryGetValue(type, out var methods)) return methods;

            var found = new List<MethodInfo>();
            var overridden = new HashSet<RuntimeMethodHandle>();
            // Private methods of a base class are returned only when asking that class, hence the walk up.
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                var declared = t.GetMethods(Flags)
                    .Where(m => m.IsDefined(typeof(ButtonAttribute), true))
                    .OrderByDescending(m => m.MetadataToken);
                foreach (var method in declared)
                {
                    // An override and the virtual method it overrides are one button: the most derived one.
                    if (overridden.Add(method.GetBaseDefinition().MethodHandle)) found.Add(method);
                }
            }

            found.Reverse();
            methods = found.ToArray();
            cache[type] = methods;
            return methods;
        }

        public static ButtonAttribute GetAttribute(MethodInfo method) => method.GetCustomAttribute<ButtonAttribute>(true);

        public static string GetLabel(MethodInfo method) =>
            GetAttribute(method)?.Name ?? StringExtension.Prettify(method.Name);

        public static bool IsEnabled(MethodInfo method) => GetAttribute(method)?.Mode switch
        {
            ButtonMode.PlayMode => Application.isPlaying,
            ButtonMode.EditMode => !Application.isPlaying,
            _ => true,
        };

        /// <summary>Parameter defaults: the declared default value, otherwise the type's default.</summary>
        public static object[] CreateArguments(MethodInfo method)
        {
            var parameters = method.GetParameters();
            var args = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var type = parameters[i].ParameterType;
                var value = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                if (value != null && type.IsEnum) value = Enum.ToObject(type, value);
                if (value == null && type.IsValueType) value = Activator.CreateInstance(type);
                if (value == null && type == typeof(string)) value = string.Empty;
                args[i] = value;
            }

            return args;
        }

        /// <summary>
        /// Calls the method and returns its result; exceptions are logged, not thrown. A returned
        /// <see cref="IEnumerator"/> runs as a coroutine of the target when it is a MonoBehaviour in Play Mode.
        /// </summary>
        public static object Invoke(MethodInfo method, object target, object[] args = null)
        {
            try
            {
                var result = method.Invoke(method.IsStatic ? null : target, args);
                if (result is IEnumerator routine && target is MonoBehaviour behaviour && Application.isPlaying)
                {
                    behaviour.StartCoroutine(routine);
                }

                return result;
            }
            catch (TargetInvocationException e)
            {
                Debug.LogException(e.InnerException ?? e, target as Object);
                return null;
            }
        }
    }
}
