using System.Collections.Generic;
using Core.Pooling;
using Core.Utilities;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Core.UI
{
    public class UIManager : Singleton<UIManager>
    {
        [ShowInInspector, ReadOnly] private Dictionary<UIScreenLayer, UIScreen> screens = new();

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

            panelPrefab.Load(1, screens[panelPrefab.Layer].transform);
            var panelInstance = panelPrefab.Spawn(screens[panelPrefab.Layer].transform);
            panelInstance.IsInstance = true;
            panelInstance.Prefab = panelPrefab;
            panelPrefab.Instance = panelInstance;
        }

        public void ClosePanel(UIPanel panel)
        {
            if (panel.Instance != null)
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

        public void DespawnPanel(UIPanel panel)
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
    }
}