using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using CoreButtonAttribute = Core.Utilities.Inspector.ButtonAttribute;
using CoreButtonMode = Core.Utilities.Inspector.ButtonMode;

namespace Core.Editor.Odin
{
    /// <summary>
    /// Odin draws objects with its own editor, so Core's fallback button editor never runs for them; this turns
    /// Core's <c>[Button]</c> into Odin's.
    /// </summary>
    internal sealed class ButtonAttributeProcessor : OdinAttributeProcessor
    {
        public override bool CanProcessSelfAttributes(InspectorProperty property) => false;

        public override bool CanProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member) =>
            member is MethodInfo && member.IsDefined(typeof(CoreButtonAttribute), true);

        public override void ProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member,
            List<Attribute> attributes)
        {
            if (attributes.Exists(a => a is ButtonAttribute)) return;

            var button = member.GetCustomAttribute<CoreButtonAttribute>(true);
            attributes.Add(button.Name == null ? new ButtonAttribute() : new ButtonAttribute(button.Name));
            if (button.Mode == CoreButtonMode.PlayMode) attributes.Add(new DisableInEditorModeAttribute());
            else if (button.Mode == CoreButtonMode.EditMode) attributes.Add(new DisableInPlayModeAttribute());
        }
    }
}
