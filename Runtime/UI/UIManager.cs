using System.Collections.Generic;
using System.Linq;
using Core.Pooling;
using Core.Utilities;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Core.UI
{
    public class UIManager : Singleton<UIManager>
    {
        [ShowInInspector, ReadOnly] private Dictionary<UIScreenLayer, UIScreen> screens = new();

        private static readonly List<UIPanel> panels = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeInitializeOnLoadMethod()
        {
            panels.Clear();
        }

        public void RegisterScreen(UIScreen uiScreen)
        {
            screens[uiScreen.layer] = uiScreen;
        }

        public void OpenPanel(UIPanel panelPrefab)
        {
            if (!screens.ContainsKey(panelPrefab.Layer))
            {
                Debug.LogError($"UIScreen:{panelPrefab.Layer} not registered");
                return;
            }

            if (panelPrefab.IsInstance)
            {
                Debug.LogError($"Cannot open instantiated panel");
                return;
            }

            if (panels.Any(p => p.Prefab == panelPrefab || p.name.Equals(panelPrefab.name)))
            {
                Debug.LogWarning($"Panel already opened");
                return;
            }

            panelPrefab.Preload(1, screens[panelPrefab.Layer].transform);
            var panelInstance = panelPrefab.Spawn(screens[panelPrefab.Layer].transform);
            panelInstance.IsInstance = true;
            panelInstance.Prefab = panelPrefab;
            panelPrefab.Instance = panelInstance;
        }

        public static void ClosePanel(UIPanel panel)
        {
            if (panel.Instance)
            {
                panel.Instance.Prefab = null;
                panel.Instance.PlayCloseTransition();
                panel.Instance = null;
            }
            else if (panel.IsInstance)
            {
                panel.PlayCloseTransition();
            }
        }

        public static void DespawnPanel(UIPanel panel)
        {
            if (panel.Instance != null)
            {
                panel.Instance.Despawn();
            }
            else
            {
                panel.Despawn();
            }
        }

        public static void Register(UIPanel uiPanel)
        {
            uiPanel.IsInstance = true;
            panels.Add(uiPanel);
        }

        public static void Unregister(UIPanel uiPanel)
        {
            panels.Remove(uiPanel);
        }
    }
}