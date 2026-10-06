using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Core.Utilities.Inspector
{
    public enum InspectorMemberKind
    {
        /// <summary>A field or property.</summary>
        Value,

        /// <summary>An element of a list or array.</summary>
        Element,

        /// <summary>A <see cref="ButtonAttribute"/> method.</summary>
        Button,

        /// <summary>Made by code with <see cref="InspectorMember.Custom"/>.</summary>
        Custom,
    }

    /// <summary>
    /// One row of a runtime inspector: reads and writes its value through the owning object, so it always shows the
    /// current state. A struct owner is written back after a change, so nested structs and list elements edit in place.
    /// </summary>
    public sealed class InspectorMember
    {
        /// <summary>Lists longer than this show only their first elements.</summary>
        public const int MaxElements = 200;

        private readonly Func<object> owner;
        private readonly Action<object> writeOwner;
        private readonly FieldInfo field;
        private readonly PropertyInfo property;
        private readonly MethodInfo onChanged;
        private readonly Func<object> customGet;
        private readonly Action<object> customSet;
        private readonly int index = -1;

        internal InspectorMember(MemberInfo member, Func<object> owner, Action<object> writeOwner, int depth,
            bool readOnlyParent = false)
        {
            this.owner = owner;
            this.writeOwner = writeOwner;
            Member = member;
            Name = member.Name;
            Depth = depth;
            Tooltip = member.GetCustomAttribute<TooltipAttribute>(true)?.tooltip;
            var inspect = member.GetCustomAttribute<InspectAttribute>(true);

            switch (member)
            {
                case FieldInfo f:
                    field = f;
                    Type = f.FieldType;
                    Kind = InspectorMemberKind.Value;
                    IsReadOnly = f.IsInitOnly || f.IsLiteral;
                    break;
                case PropertyInfo p:
                    property = p;
                    Type = p.PropertyType;
                    Kind = InspectorMemberKind.Value;
                    IsReadOnly = p.SetMethod == null;
                    break;
                case MethodInfo m:
                    Method = m;
                    Type = m.ReturnType;
                    Kind = InspectorMemberKind.Button;
                    Label = ButtonMethods.GetLabel(m);
                    return;
                default:
                    throw new ArgumentException($"{member.Name} is not a field, property or method", nameof(member));
            }

            Label = string.IsNullOrEmpty(inspect?.Label) ? InspectorModel.Nicify(Name) : inspect.Label;
            IsReadOnly |= readOnlyParent || inspect is { ReadOnly: true };
            if (!string.IsNullOrEmpty(inspect?.OnChanged)) onChanged = FindCallback(member.DeclaringType, inspect.OnChanged);
        }

        private InspectorMember(Type elementType, int index, Func<object> owner, int depth, bool readOnly)
        {
            this.owner = owner;
            this.index = index;
            Name = $"[{index}]";
            Label = $"Element {index}";
            Type = elementType;
            Kind = InspectorMemberKind.Element;
            Depth = depth;
            IsReadOnly = readOnly;
        }

        private InspectorMember(string label, Type type, Func<object> get, Action<object> set)
        {
            Name = label;
            Label = label;
            Type = type;
            Kind = InspectorMemberKind.Custom;
            customGet = get;
            customSet = set;
            IsReadOnly = set == null;
        }

        /// <summary>A row that is not a member, e.g. a method parameter or a computed value on a debug page.</summary>
        public static InspectorMember Custom(string label, Type type, Func<object> get, Action<object> set = null) =>
            new(label, type, get, set);

        public string Name { get; }
        public string Label { get; }
        public string Tooltip { get; }

        /// <summary>The declared type; for a button, the return type.</summary>
        public Type Type { get; }

        public InspectorMemberKind Kind { get; }
        public bool IsReadOnly { get; }

        /// <summary>Nesting level: 0 for members of the inspected object.</summary>
        public int Depth { get; }

        /// <summary>The field, property or method; null for list elements and custom rows.</summary>
        public MemberInfo Member { get; }

        public MethodInfo Method { get; }

        public object Owner => owner?.Invoke();

        public T GetAttribute<T>() where T : Attribute => Member?.GetCustomAttribute<T>(true);

        public object GetValue()
        {
            try
            {
                switch (Kind)
                {
                    case InspectorMemberKind.Custom:
                        return customGet?.Invoke();
                    case InspectorMemberKind.Element:
                        return owner() is IList list && index < list.Count ? list[index] : null;
                    case InspectorMemberKind.Value:
                        var target = field is { IsStatic: true } || property?.GetMethod?.IsStatic == true ? null : owner();
                        return field != null ? field.GetValue(target) : property.GetValue(target);
                    default:
                        return null;
                }
            }
            catch (Exception exception)
            {
                // A throwing getter shows as its error rather than breaking the whole inspector.
                return exception.InnerException ?? exception;
            }
        }

        /// <summary>Writes the value and runs the <see cref="InspectAttribute.OnChanged"/> callback.</summary>
        /// <returns>False when the row is read-only or writing failed (logged).</returns>
        public bool SetValue(object value)
        {
            if (IsReadOnly || Kind == InspectorMemberKind.Button) return false;

            try
            {
                if (Kind == InspectorMemberKind.Custom)
                {
                    customSet(value);
                    return true;
                }

                var isStatic = field is { IsStatic: true } || property?.SetMethod?.IsStatic == true;
                var target = isStatic ? null : owner();
                if (Kind == InspectorMemberKind.Element)
                {
                    if (target is not IList list || index >= list.Count) return false;
                    list[index] = value;
                    return true;
                }

                if (field != null) field.SetValue(target, value);
                else property.SetValue(target, value);

                // A boxed struct was changed, not the original: hand the box back to whoever holds it.
                if (target != null && target.GetType().IsValueType) writeOwner?.Invoke(target);
                if (onChanged != null) onChanged.Invoke(onChanged.IsStatic ? null : target, null);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception is TargetInvocationException { InnerException: { } inner } ? inner : exception);
                return false;
            }
        }

        /// <summary>Whether the current value has rows of its own: a list or a nested serializable object.</summary>
        public bool HasChildren
        {
            get
            {
                if (Kind == InspectorMemberKind.Button) return false;
                var value = GetValue();
                return value != null && !(value is Exception) &&
                       (InspectorModel.IsList(value.GetType()) || InspectorModel.IsNested(value.GetType()));
            }
        }

        /// <summary>Rows of the current value; read again each call, so a list's rows follow its length.</summary>
        public IReadOnlyList<InspectorMember> GetChildren()
        {
            var value = GetValue();
            if (value == null || value is Exception || Kind == InspectorMemberKind.Button)
                return Array.Empty<InspectorMember>();

            var type = value.GetType();
            if (value is IList list && InspectorModel.IsList(type))
            {
                var elementType = InspectorModel.GetElementType(type);
                var count = Mathf.Min(list.Count, MaxElements);
                var elements = new List<InspectorMember>(count);
                for (var i = 0; i < count; i++)
                {
                    elements.Add(new InspectorMember(elementType, i, GetValue, Depth + 1, IsReadOnly || list.IsReadOnly));
                }

                return elements;
            }

            if (!InspectorModel.IsNested(type)) return Array.Empty<InspectorMember>();
            Action<object> write = type.IsValueType ? v => SetValue(v) : null;
            var members = InspectorModel.GetMembers(type, GetValue, write, Depth + 1);
            if (!IsReadOnly) return members;

            var readOnly = new List<InspectorMember>(members.Count);
            foreach (var member in members)
            {
                readOnly.Add(member.Member is MethodInfo
                    ? member
                    : new InspectorMember(member.Member, GetValue, write, Depth + 1, true));
            }

            return readOnly;
        }

        /// <summary>Calls the button's method; exceptions are logged.</summary>
        public object Invoke(object[] args = null)
        {
            if (Method == null) return null;
            var target = Method.IsStatic ? null : owner();
            var result = ButtonMethods.Invoke(Method, target, args);
            if (target != null && target.GetType().IsValueType) writeOwner?.Invoke(target);
            return result;
        }

        public override string ToString() => $"{Label} ({Type?.Name})";

        private static MethodInfo FindCallback(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var method = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                               BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                    null, Type.EmptyTypes, null);
                if (method != null) return method;
            }

            Debug.LogError($"[Inspector] {type.Name} has no parameterless method {name} for [Inspect(OnChanged)].");
            return null;
        }
    }
}
