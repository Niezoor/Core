#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core.Bootstrap
{
    /// <summary>
    /// How the editor puts <see cref="BootStepAttribute"/> steps into <see cref="BootSettings"/>, apart from finding
    /// the types (<c>Core.Editor.BootStepSync</c>), so tests can hand it types of their own.
    /// </summary>
    internal static class BootSteps
    {
        /// <summary>
        /// Adds each auto-added step missing from its phase and sorts every phase by order. Steps already there are
        /// kept as they are - their switches and fields are the project's.
        /// </summary>
        /// <param name="steps">Step types and their attributes.</param>
        /// <param name="added">Receives the display names of the steps added.</param>
        /// <returns>Whether <paramref name="settings"/> changed.</returns>
        public static bool Merge(BootSettings settings, IReadOnlyDictionary<Type, BootStepAttribute> steps,
            List<string> added)
        {
            var changed = Merge(settings.Initializers, steps, added);
            changed |= Merge(settings.Services, steps, added);
            changed |= Merge(settings.SplashTasks, steps, added);
            return changed;
        }

        public static int OrderOf(object step, IReadOnlyDictionary<Type, BootStepAttribute> steps) =>
            step != null && steps.TryGetValue(step.GetType(), out var attribute) ? attribute.Order : BootOrder.Default;

        /// <summary>Why <paramref name="type"/> can't be added on its own; null when it can.</summary>
        public static string Problem(Type type)
        {
            if (!typeof(BootInitializer).IsAssignableFrom(type) && !typeof(BootService).IsAssignableFrom(type) &&
                !typeof(SplashTask).IsAssignableFrom(type))
                return "is not a BootInitializer, BootService or SplashTask";
            if (type.IsAbstract || type.ContainsGenericParameters) return "is abstract or generic";
            if (!type.IsSerializable) return "is not [Serializable], so BootSettings can't store it";
            if (type.GetConstructor(Type.EmptyTypes) == null) return "has no parameterless constructor";
            return null;
        }

        private static bool Merge<T>(List<T> list, IReadOnlyDictionary<Type, BootStepAttribute> steps,
            List<string> added) where T : BootEntry
        {
            var changed = false;
            var missing = steps
                .Where(s => s.Value.AutoAdd && typeof(T).IsAssignableFrom(s.Key) &&
                            !list.Any(e => e != null && e.GetType() == s.Key))
                .OrderBy(s => s.Value.Order)
                .ThenBy(s => s.Key.FullName, StringComparer.Ordinal)
                .ToList();

            foreach (var step in missing)
            {
                var problem = Problem(step.Key);
                if (problem != null)
                {
                    Debug.LogWarning($"[Boot] {step.Key.FullName} has [BootStep] but {problem}; it is not added.");
                    continue;
                }

                var instance = (T)Activator.CreateInstance(step.Key);
                list.Add(instance);
                added.Add(instance.DisplayName);
                changed = true;
            }

            // OrderBy is stable: steps of equal order keep the order the project gave them.
            var sorted = list.OrderBy(e => OrderOf(e, steps)).ToList();
            if (sorted.SequenceEqual(list)) return changed;

            list.Clear();
            list.AddRange(sorted);
            return true;
        }
    }
}
#endif
