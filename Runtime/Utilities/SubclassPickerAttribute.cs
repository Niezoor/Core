using System;
using UnityEngine;

namespace Core.Utilities
{
    /// <summary>
    /// For a <c>[SerializeReference]</c> field or list: the inspector shows a dropdown of every concrete,
    /// serializable subclass of the field's type, so content can be extended from any assembly, the game's included.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SubclassPickerAttribute : PropertyAttribute
    { }
}
