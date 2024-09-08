using System;
using UnityEngine;

namespace Core.Utilities.Settings
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class SettingsPathAttribute : PropertyAttribute
    {
        public string Path;

        public SettingsPathAttribute(string path)
        {
            Path = path;
        }
    }
}