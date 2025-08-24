using System;
using Core.Utilities;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Core.UI
{
    public class ScreenWatcher : Singleton<ScreenWatcher>
    {
        public event Action OnScreenChanged;
        public event Action OnSafeAreaChanged;

        [NonSerialized, ShowInInspector, ReadOnly] public Vector2Int ScreenSize = new(Screen.width, Screen.height);
        [NonSerialized, ShowInInspector, ReadOnly] public Rect SafeArea;

        private void Update()
        {
            if (ScreenSize.x != Screen.width || ScreenSize.y != Screen.height)
            {
                ScreenSize.x = Screen.width;
                ScreenSize.y = Screen.height;
                OnScreenChanged?.Invoke();
            }

            if (SafeArea != Screen.safeArea)
            {
                SafeArea = Screen.safeArea;
                OnSafeAreaChanged?.Invoke();
            }
        }
    }
}