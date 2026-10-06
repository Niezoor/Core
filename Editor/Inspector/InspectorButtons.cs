using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Core.Utilities.Inspector;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Core.Editor.Inspector
{
    /// <summary>
    /// Draws <see cref="ButtonAttribute"/> methods under the default inspector of every MonoBehaviour and
    /// ScriptableObject that has no custom editor. A custom editor adds them itself with <see cref="Create"/>.
    /// </summary>
    public static class InspectorButtons
    {
        // Typed parameter values are kept per method while the editor runs, so they survive reselecting the object.
        private static readonly Dictionary<MethodInfo, object[]> arguments = new();

        /// <summary>The buttons of the targets' type, or null when it has none.</summary>
        public static VisualElement Create(Object[] targets)
        {
            if (targets.Length == 0 || !targets[0]) return null;
            var methods = ButtonMethods.Get(targets[0].GetType());
            if (methods.Count == 0) return null;

            var root = new VisualElement();
            root.style.marginTop = 6f;
            var buttons = new List<(Button button, MethodInfo method)>();
            foreach (var method in methods)
            {
                root.Add(CreateButton(method, targets, out var button));
                buttons.Add((button, method));
            }

            void Refresh()
            {
                foreach (var (button, method) in buttons) button.SetEnabled(ButtonMethods.IsEnabled(method));
            }

            void OnPlayModeChanged(PlayModeStateChange _) => Refresh();
            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Refresh();
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.playModeStateChanged -= OnPlayModeChanged);
            return root;
        }

        private static VisualElement CreateButton(MethodInfo method, Object[] targets, out Button button)
        {
            var label = ButtonMethods.GetLabel(method);
            var parameters = method.GetParameters();
            if (!arguments.TryGetValue(method, out var args) || args.Length != parameters.Length)
            {
                arguments[method] = args = ButtonMethods.CreateArguments(method);
            }

            button = new Button(() => Invoke(method, label, targets, args)) { text = label };
            button.style.minHeight = 22f;
            var mode = ButtonMethods.GetAttribute(method).Mode;
            if (mode != ButtonMode.Always) button.tooltip = mode == ButtonMode.PlayMode ? "Play Mode only" : "Edit Mode only";
            if (parameters.Length == 0) return button;

            var box = new VisualElement();
            box.AddToClassList("unity-help-box");
            box.style.flexDirection = FlexDirection.Column;
            box.style.alignItems = Align.Stretch;
            box.style.marginTop = box.style.marginBottom = 2f;
            for (var i = 0; i < parameters.Length; i++)
            {
                var index = i;
                var name = ObjectNames.NicifyVariableName(parameters[i].Name);
                var field = CreateField(parameters[i].ParameterType, name, args[i], value => args[index] = value);
                if (field == null)
                {
                    field = new Label($"{name}: {parameters[i].ParameterType.Name} is not supported");
                    button.SetEnabled(false);
                    button.tooltip = "A parameter type has no field";
                }

                box.Add(field);
            }

            box.Add(button);
            return box;
        }

        private static void Invoke(MethodInfo method, string label, Object[] targets, object[] args)
        {
            if (method.IsStatic)
            {
                LogResult(method, label, ButtonMethods.Invoke(method, null, args), null);
                return;
            }

            Undo.RecordObjects(targets, label);
            foreach (var target in targets)
            {
                if (!target) continue;
                LogResult(method, label, ButtonMethods.Invoke(method, target, args), target);
                if (Application.isPlaying) continue;
                EditorUtility.SetDirty(target);
                if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
        }

        private static void LogResult(MethodInfo method, string label, object result, Object context)
        {
            var type = method.ReturnType;
            if (type == typeof(void) || typeof(IEnumerator).IsAssignableFrom(type) || typeof(Task).IsAssignableFrom(type) ||
                typeof(Awaitable).IsAssignableFrom(type)) return;
            Debug.Log($"{label}: {result ?? "null"}", context);
        }

        private static VisualElement CreateField(Type type, string label, object value, Action<object> set)
        {
            if (type == typeof(bool)) return Bind(new Toggle(label), (bool)value, set);
            if (type == typeof(int)) return Bind(new IntegerField(label), (int)value, set);
            if (type == typeof(long)) return Bind(new LongField(label), (long)value, set);
            if (type == typeof(float)) return Bind(new FloatField(label), (float)value, set);
            if (type == typeof(double)) return Bind(new DoubleField(label), (double)value, set);
            if (type == typeof(string)) return Bind(new TextField(label), (string)value ?? string.Empty, set);
            if (type == typeof(Vector2)) return Bind(new Vector2Field(label), (Vector2)value, set);
            if (type == typeof(Vector3)) return Bind(new Vector3Field(label), (Vector3)value, set);
            if (type == typeof(Vector4)) return Bind(new Vector4Field(label), (Vector4)value, set);
            if (type == typeof(Vector2Int)) return Bind(new Vector2IntField(label), (Vector2Int)value, set);
            if (type == typeof(Vector3Int)) return Bind(new Vector3IntField(label), (Vector3Int)value, set);
            if (type == typeof(Color)) return Bind(new ColorField(label), (Color)value, set);
            if (type == typeof(Rect)) return Bind(new RectField(label), (Rect)value, set);
            if (type == typeof(Bounds)) return Bind(new BoundsField(label), (Bounds)value, set);
            if (type.IsEnum)
            {
                return type.IsDefined(typeof(FlagsAttribute), false)
                    ? Bind(new EnumFlagsField(label, (Enum)value), (Enum)value, set)
                    : Bind(new EnumField(label, (Enum)value), (Enum)value, set);
            }

            if (typeof(Object).IsAssignableFrom(type))
            {
                return Bind(new ObjectField(label) { objectType = type, allowSceneObjects = true }, (Object)value, set);
            }

            return null;
        }

        private static VisualElement Bind<T>(BaseField<T> field, T value, Action<object> set)
        {
            field.AddToClassList(BaseField<T>.alignedFieldUssClassName);
            field.SetValueWithoutNotify(value);
            field.RegisterValueChangedCallback(e => set(e.newValue));
            return field;
        }
    }

    internal abstract class ButtonsEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            if (serializedObject.FindProperty("m_Script")?.objectReferenceValue == null)
            {
                root.Add(new HelpBox("The associated script can not be loaded. Fix compile errors and assign a valid script.",
                    HelpBoxMessageType.Warning));
            }

            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            var buttons = InspectorButtons.Create(targets);
            if (buttons != null) root.Add(buttons);
            return root;
        }
    }

    // Fallback: used only for types without their own editor (and never when Odin draws the type).
    [CustomEditor(typeof(MonoBehaviour), true, isFallback = true), CanEditMultipleObjects]
    internal sealed class MonoBehaviourButtonsEditor : ButtonsEditor
    { }

    [CustomEditor(typeof(ScriptableObject), true, isFallback = true), CanEditMultipleObjects]
    internal sealed class ScriptableObjectButtonsEditor : ButtonsEditor
    { }
}
