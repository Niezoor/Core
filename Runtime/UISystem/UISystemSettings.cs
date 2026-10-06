using System;
using System.Collections.Generic;
using Core.Display;
using Core.Utilities.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.UISystem
{
    /// <summary>Panel, theme and the screen registry (which UXML each <see cref="UIScreen"/> uses).</summary>
    [SettingsMenu("Core/UI System")]
    public sealed class UISystemSettings : SettingsAsset<UISystemSettings>
    {
        [Tooltip("Copied at runtime. Empty: Unity's default runtime theme.")]
        [SerializeField] private PanelSettings panelSettings;

        [Tooltip("The game's theme, added to the root of every screen.")]
        [SerializeField] private List<StyleSheet> styleSheets = new();

        [Tooltip("Scale the panel by ScreenProfile.Scale (density × player UI scale), so UXML sizes are in dp.")]
        [SerializeField] private bool scaleWithProfile = true;

        [Tooltip("Filled by the editor: a screen class and a UXML with the same name are paired automatically.")]
        [SerializeField] private List<ScreenEntry> screens = new();

        [Header("Debug menu")]
        [Tooltip("Development builds and the editor always have it.")]
        [SerializeField] private bool debugMenuInRelease;

        public PanelSettings PanelSettings => panelSettings;
        public IReadOnlyList<StyleSheet> StyleSheets => styleSheets;
        public bool ScaleWithProfile => scaleWithProfile;
        public bool DebugMenuInRelease => debugMenuInRelease;
        public IReadOnlyList<ScreenEntry> Screens => screens;

        internal List<ScreenEntry> EditableScreens => screens;

        public ScreenEntry FindEntry(Type type)
        {
            foreach (var entry in screens)
            {
                if (entry.Type == type) return entry;
            }

            return null;
        }
    }

    /// <summary>A screen class and its views: the default and variants for particular screens.</summary>
    [Serializable]
    public sealed class ScreenEntry
    {
        [SerializeField, ScreenType] private string type;
        [SerializeField] private VisualTreeAsset view;

        [Tooltip("Checked in order; the first match replaces the default view.")]
        [SerializeField] private List<ScreenVariant> variants = new();

        [NonSerialized] private Type resolved;
        [NonSerialized] private string resolvedName;

        public ScreenEntry(Type type, VisualTreeAsset view)
        {
            this.type = ScreenTypeAttribute.Serialize(type);
            this.view = view;
        }

        /// <summary>The screen class; null when it no longer exists.</summary>
        public Type Type
        {
            get
            {
                if (resolvedName != type)
                {
                    resolvedName = type;
                    resolved = string.IsNullOrEmpty(type) ? null : Type.GetType(type);
                }

                return resolved;
            }
        }

        public string TypeName => type;
        public VisualTreeAsset View => view;
        public IReadOnlyList<ScreenVariant> Variants => variants;

        internal void SetView(VisualTreeAsset value) => view = value;

        public VisualTreeAsset GetView(in ScreenState state)
        {
            foreach (var variant in variants)
            {
                if (variant.View && variant.Condition.Matches(state)) return variant.View;
            }

            return view;
        }
    }

    [Serializable]
    public sealed class ScreenVariant
    {
        [SerializeField] private ScreenCondition condition = new();
        [SerializeField] private VisualTreeAsset view;

        public ScreenCondition Condition => condition;
        public VisualTreeAsset View => view;
    }

    /// <summary>A string field holding a <see cref="UIScreen"/> class; the inspector shows a list of them.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ScreenTypeAttribute : PropertyAttribute
    {
        /// <summary>"Namespace.Type, Assembly": survives assembly version changes, unlike the full qualified name.</summary>
        public static string Serialize(Type type) => type == null ? string.Empty : $"{type.FullName}, {type.Assembly.GetName().Name}";
    }
}
