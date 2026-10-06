using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Core.Utilities.Inspector;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Core.UISystem
{
    /// <summary>What a control can ask of the inspector it is in.</summary>
    public sealed class InspectorContext
    {
        private readonly Action<object> select;

        public InspectorContext(Action<object> select)
        {
            this.select = select;
        }

        /// <summary>Opens the object in the inspector (drill-in), if the inspector supports it.</summary>
        public void Select(object target) => select?.Invoke(target);
    }

    /// <summary>Builds a control for a row; <paramref name="refresh"/> re-reads the value into it.</summary>
    public delegate VisualElement InspectorControlFactory(InspectorMember member, InspectorContext context,
        out Action refresh);

    /// <summary>
    /// Controls of the runtime inspector, by value type. A game adds its own with <see cref="Register"/>; the last
    /// registration that matches wins over earlier ones and the built-ins.
    /// </summary>
    public static class InspectorControls
    {
        public const string FieldClass = "ui-inspector__field";
        public const string ButtonClass = "ui-inspector__button";

        private static readonly List<(Func<Type, bool> match, InspectorControlFactory factory)> registered = new();

        public static void Register(Func<Type, bool> match, InspectorControlFactory factory) =>
            registered.Insert(0, (match, factory));

        public static void Register<T>(InspectorControlFactory factory) => Register(t => t == typeof(T), factory);

        /// <summary>A control for a value row; a read-only text for types without one.</summary>
        public static VisualElement Create(InspectorMember member, InspectorContext context, out Action refresh)
        {
            foreach (var (match, factory) in registered)
            {
                if (match(member.Type)) return factory(member, context, out refresh);
            }

            return CreateBuiltIn(member, context, out refresh) ?? CreateText(member, context, out refresh);
        }

        /// <summary>A button for a <see cref="ButtonAttribute"/> method, with fields for its parameters.</summary>
        public static VisualElement CreateButton(InspectorMember member, InspectorContext context)
        {
            var method = member.Method;
            var args = ButtonMethods.CreateArguments(method);
            var button = new Button(() => LogResult(member, member.Invoke(args))) { text = member.Label };
            button.AddToClassList(ButtonClass);
            button.SetEnabled(ButtonMethods.IsEnabled(method));
            var parameters = method.GetParameters();
            if (parameters.Length == 0) return button;

            var box = new VisualElement();
            box.AddToClassList("ui-inspector__method");
            for (var i = 0; i < parameters.Length; i++)
            {
                var index = i;
                var parameter = InspectorMember.Custom(InspectorModel.Nicify(parameters[i].Name),
                    parameters[i].ParameterType, () => args[index], value => args[index] = value);
                box.Add(Create(parameter, context, out _));
            }

            box.Add(button);
            return box;
        }

        public static string Format(object value) => value switch
        {
            null => "null",
            Exception exception => $"<error> {exception.Message}",
            Object unityObject => unityObject ? $"{unityObject.name} ({unityObject.GetType().Name})" : "None",
            float number => number.ToString("0.###", CultureInfo.InvariantCulture),
            double number => number.ToString("0.###", CultureInfo.InvariantCulture),
            string text => text,
            ICollection collection => $"{value.GetType().Name} ({collection.Count})",
            _ => value.ToString(),
        };

        private static VisualElement CreateBuiltIn(InspectorMember m, InspectorContext context, out Action refresh)
        {
            var type = m.Type;
            var label = m.Label;
            GetRange(m, out var min, out var max, out var hasRange);
            refresh = null;

            if (type == typeof(bool)) return Bind(new Toggle(label), m, out refresh);
            if (type == typeof(int))
            {
                return hasRange
                    ? Bind(new SliderInt(label, (int)min, (int)max) { showInputField = true }, m, out refresh)
                    : Bind(new IntegerField(label), m, out refresh);
            }

            if (type == typeof(float))
            {
                return hasRange
                    ? Bind(new Slider(label, min, max) { showInputField = true }, m, out refresh)
                    : Bind(new FloatField(label), m, out refresh);
            }

            if (type == typeof(long)) return Bind(new LongField(label), m, out refresh);
            if (type == typeof(double)) return Bind(new DoubleField(label), m, out refresh);
            if (type == typeof(uint)) return Bind(new UnsignedIntegerField(label), m, out refresh);
            if (type == typeof(short) || type == typeof(ushort) || type == typeof(byte) || type == typeof(sbyte))
            {
                return Bind(new IntegerField(label), m, out refresh, v => v == null ? 0 : Convert.ToInt32(v),
                    v => Convert.ChangeType(v, type, CultureInfo.InvariantCulture));
            }

            if (type == typeof(string))
            {
                var multiline = m.GetAttribute<TextAreaAttribute>() != null || m.GetAttribute<MultilineAttribute>() != null;
                return Bind(new TextField(label) { multiline = multiline, isDelayed = true }, m, out refresh,
                    v => v as string ?? string.Empty);
            }

            if (type.IsEnum)
            {
                if (type.IsDefined(typeof(FlagsAttribute), false)) return CreateFlags(m, out refresh);
                return Bind(new EnumField(label, (Enum)Activator.CreateInstance(type)), m, out refresh,
                    v => v as Enum ?? (Enum)Activator.CreateInstance(type));
            }

            if (type == typeof(Vector2)) return Bind(new Vector2Field(label), m, out refresh);
            if (type == typeof(Vector3)) return Bind(new Vector3Field(label), m, out refresh);
            if (type == typeof(Vector4)) return Bind(new Vector4Field(label), m, out refresh);
            if (type == typeof(Vector2Int)) return Bind(new Vector2IntField(label), m, out refresh);
            if (type == typeof(Vector3Int)) return Bind(new Vector3IntField(label), m, out refresh);
            if (type == typeof(Rect)) return Bind(new RectField(label), m, out refresh);
            if (type == typeof(Bounds)) return Bind(new BoundsField(label), m, out refresh);
            if (type == typeof(Quaternion))
            {
                return Bind(new Vector3Field(label), m, out refresh, v => v is Quaternion q ? q.eulerAngles : Vector3.zero,
                    v => Quaternion.Euler(v));
            }

            if (type == typeof(Color)) return CreateColor(m, out refresh);
            if (typeof(Object).IsAssignableFrom(type)) return CreateObjectReference(m, context, out refresh);
            return null;
        }

        private static void GetRange(InspectorMember member, out float min, out float max, out bool hasRange)
        {
            var range = member.GetAttribute<RangeAttribute>();
            var inspect = member.GetAttribute<InspectAttribute>();
            if (range != null)
            {
                min = range.min;
                max = range.max;
            }
            else if (inspect != null && !float.IsNaN(inspect.Min) && !float.IsNaN(inspect.Max))
            {
                min = inspect.Min;
                max = inspect.Max;
            }
            else
            {
                min = max = 0f;
                hasRange = false;
                return;
            }

            hasRange = true;
        }

        /// <summary>Two-way binding of a field to a row; the value is not overwritten while the player types in it.</summary>
        public static VisualElement Bind<T>(BaseField<T> field, InspectorMember member, out Action refresh,
            Func<object, T> toField = null, Func<T, object> fromField = null)
        {
            toField ??= v => v is T t ? t : default;
            fromField ??= v => v;
            field.AddToClassList(FieldClass);
            field.AddToClassList(BaseField<T>.alignedFieldUssClassName);
            field.tooltip = member.Tooltip;
            field.SetEnabled(!member.IsReadOnly);

            var shown = default(T);
            var hasShown = false;
            refresh = () =>
            {
                if (IsEditing(field)) return;
                var value = toField(member.GetValue());
                if (hasShown && EqualityComparer<T>.Default.Equals(value, shown)) return;
                shown = value;
                hasShown = true;
                field.SetValueWithoutNotify(value);
            };
            field.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != field) return;
                if (member.SetValue(fromField(evt.newValue))) shown = evt.newValue;
            });
            refresh();
            return field;
        }

        private static bool IsEditing(VisualElement field)
        {
            var focused = field.focusController?.focusedElement as VisualElement;
            return focused != null && (focused == field || field.Contains(focused)) && Panels.IsTextInput(focused);
        }

        private static VisualElement CreateFlags(InspectorMember member, out Action refresh)
        {
            var type = member.Type;
            var foldout = new Foldout { text = member.Label, value = false };
            foldout.AddToClassList(FieldClass);
            var toggles = new List<(Toggle toggle, long bit)>();
            foreach (var value in Enum.GetValues(type))
            {
                var bit = Convert.ToInt64(value);
                if (bit == 0 || (bit & (bit - 1)) != 0) continue;
                var toggle = new Toggle(Enum.GetName(type, value));
                toggle.SetEnabled(!member.IsReadOnly);
                toggle.RegisterValueChangedCallback(evt =>
                {
                    var current = Convert.ToInt64(member.GetValue() ?? 0);
                    current = evt.newValue ? current | bit : current & ~bit;
                    member.SetValue(Enum.ToObject(type, current));
                });
                toggles.Add((toggle, bit));
                foldout.Add(toggle);
            }

            refresh = () =>
            {
                var current = member.GetValue() is Enum value ? Convert.ToInt64(value) : 0L;
                foldout.text = $"{member.Label}: {member.GetValue()}";
                foreach (var (toggle, bit) in toggles) toggle.SetValueWithoutNotify((current & bit) != 0);
            };
            refresh();
            return foldout;
        }

        private static VisualElement CreateColor(InspectorMember member, out Action refresh)
        {
            var row = new VisualElement();
            row.AddToClassList("ui-inspector__color");
            var swatch = new VisualElement();
            swatch.AddToClassList("ui-inspector__swatch");
            var field = Bind(new Vector4Field(member.Label), member, out var refreshField,
                v => v is Color c ? (Vector4)c : Vector4.one, v => (Color)v);
            row.Add(field);
            row.Add(swatch);
            refresh = () =>
            {
                refreshField();
                swatch.style.backgroundColor = member.GetValue() is Color c ? c : Color.clear;
            };
            refresh();
            return row;
        }

        private static VisualElement CreateObjectReference(InspectorMember member, InspectorContext context,
            out Action refresh)
        {
            var row = CreateLabeledRow(member.Label, out var valueButton);
            row.tooltip = member.Tooltip;
            valueButton.clicked += () =>
            {
                if (member.GetValue() is Object target && target) context.Select(target);
            };
            refresh = () => valueButton.text = Format(member.GetValue());
            refresh();
            return row;
        }

        private static VisualElement CreateText(InspectorMember member, InspectorContext context, out Action refresh)
        {
            var row = CreateLabeledRow(member.Label, out var valueButton);
            row.tooltip = member.Tooltip;
            // Any other object can still be opened on its own page.
            valueButton.clicked += () =>
            {
                var value = member.GetValue();
                if (value != null && !(value is Exception) && !value.GetType().IsPrimitive && !(value is string))
                    context.Select(value);
            };
            refresh = () => valueButton.text = Format(member.GetValue());
            refresh();
            return row;
        }

        private static VisualElement CreateLabeledRow(string label, out Button valueButton)
        {
            var row = new VisualElement();
            row.AddToClassList(FieldClass);
            row.AddToClassList("ui-inspector__row");
            var title = new Label(label);
            title.AddToClassList("ui-inspector__label");
            valueButton = new Button();
            valueButton.AddToClassList("ui-inspector__value");
            row.Add(title);
            row.Add(valueButton);
            return row;
        }

        private static void LogResult(InspectorMember member, object result)
        {
            var type = member.Type;
            if (type == typeof(void) || typeof(IEnumerator).IsAssignableFrom(type) || typeof(Task).IsAssignableFrom(type) ||
                typeof(Awaitable).IsAssignableFrom(type)) return;
            Debug.Log($"{member.Label}: {Format(result)}");
        }

        // Domain reload is off in Enter Play Mode settings, so nothing static may survive into the next session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => registered.Clear();
    }
}
