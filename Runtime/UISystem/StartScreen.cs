using System;
using UnityEngine;

namespace Core.UISystem
{
    /// <summary>Opens a screen when the scene starts, e.g. the main menu in the menu scene.</summary>
    [AddComponentMenu("Core/UI System/Start Screen")]
    public sealed class StartScreen : MonoBehaviour
    {
        [SerializeField, ScreenType] private string screen;

        [Tooltip("Close whatever the previous scene left open first.")]
        [SerializeField] private bool clearStack = true;

        private void Start()
        {
            var type = string.IsNullOrEmpty(screen) ? null : Type.GetType(screen);
            if (type == null)
            {
                Debug.LogError($"[UISystem] {name}: the start screen \"{screen}\" does not exist.", this);
                return;
            }

            if (clearStack) Screens.Clear();
            Screens.Push(type);
        }
    }
}
