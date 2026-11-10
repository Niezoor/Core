#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core.Bootstrap
{
    /// <summary>
    /// How the editor writes the <see cref="BootStepAttribute"/> steps into <see cref="BootSettings"/>, apart from
    /// finding the types (<c>Core.Editor.BootStepSync</c>), so tests can hand it types of their own.
    /// </summary>
    internal static class BootSteps
    {
        /// <summary>
        /// Makes every phase exactly the steps of <paramref name="steps"/> for it, in order, configured by their
        /// attributes. Instances already there are reused; anything else in the lists goes.
        /// </summary>
        /// <param name="steps">Step types and their attributes.</param>
        /// <param name="added">Receives the display names of the steps added.</param>
        /// <param name="removed">Receives the display names of the steps dropped (no attribute any more).</param>
        /// <returns>Whether <paramref name="settings"/> changed.</returns>
        public static bool Merge(BootSettings settings, IReadOnlyDictionary<Type, BootStepAttribute> steps,
            List<string> added, List<string> removed)
        {
            var changed = Merge(settings.Initializers, steps, added, removed);
            changed |= Merge(settings.Services, steps, added, removed);
            changed |= Merge(settings.SplashTasks, steps, added, removed);
            return changed;
        }

        /// <summary>Why <paramref name="type"/> can't be a boot step; null when it can.</summary>
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
            List<string> added, List<string> removed) where T : BootEntry
        {
            var wanted = steps
                .Where(s => typeof(T).IsAssignableFrom(s.Key))
                .OrderBy(s => s.Value.Order)
                .ThenBy(s => s.Key.FullName, StringComparer.Ordinal);

            var result = new List<T>();
            foreach (var step in wanted)
            {
                var problem = Problem(step.Key);
                if (problem != null)
                {
                    Debug.LogWarning($"[Boot] {step.Key.FullName} has [BootStep] but {problem}; it does not run.");
                    continue;
                }

                var instance = list.FirstOrDefault(e => e != null && e.GetType() == step.Key);
                if (instance == null)
                {
                    instance = (T)Activator.CreateInstance(step.Key);
                    added.Add(instance.DisplayName);
                }

                result.Add(instance);
            }

            foreach (var entry in list)
            {
                if (entry != null && !result.Contains(entry)) removed.Add(entry.DisplayName);
            }

            var changed = !result.SequenceEqual(list);
            foreach (var entry in result) changed |= Configure(entry, steps[entry.GetType()]);
            if (!changed) return false;

            list.Clear();
            list.AddRange(result);
            return true;
        }

        private static bool Configure(BootEntry entry, BootStepAttribute attribute)
        {
            var changed = !entry.Enabled || entry.Platforms != attribute.Platforms;
            entry.Enabled = true;
            entry.Platforms = attribute.Platforms;

            if (entry is SplashTask task)
            {
                changed |= task.Required != attribute.Required || !Mathf.Approximately(task.Timeout, attribute.Timeout);
                task.Required = attribute.Required;
                task.Timeout = attribute.Timeout;
            }

            return changed;
        }
    }
}
#endif
