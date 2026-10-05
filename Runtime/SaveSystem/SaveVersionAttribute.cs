using System;
using System.Collections.Generic;

namespace Core.SaveSystem
{
    /// <summary>
    /// Version of a type stored as a save entry. Bump it when the shape changes; an entry saved with a lower
    /// version is handed to <see cref="ISaveMigration"/>, one saved with a higher version is never overwritten.
    /// A type without the attribute is version 0.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class SaveVersionAttribute : Attribute
    {
        private static readonly Dictionary<Type, int> Cache = new();

        public int Version { get; }

        public SaveVersionAttribute(int version)
        {
            Version = version;
        }

        public static int Of(Type type)
        {
            if (Cache.TryGetValue(type, out var version)) return version;
            var attribute = (SaveVersionAttribute)GetCustomAttribute(type, typeof(SaveVersionAttribute), false);
            version = attribute?.Version ?? 0;
            Cache[type] = version;
            return version;
        }
    }

    /// <summary>
    /// Implemented by a save entry type that needs to upgrade data saved under an older <see cref="SaveVersionAttribute"/>.
    /// </summary>
    public interface ISaveMigration
    {
        /// <param name="fromVersion">Version the entry was saved with.</param>
        /// <param name="json">The entry as saved. Fields that kept their name are already filled in; this is
        /// for anything that was renamed or reshaped.</param>
        void Migrate(int fromVersion, string json);
    }
}
