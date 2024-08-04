using UnityEngine;
using UnityEngine.UI;

namespace Core.UI
{
    public class UIPanelOpenButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private UIPanel panel;

#if UNITY_EDITOR
        private void Reset()
        {
            button = GetComponent<Button>();
        }
#endif

        private void Start()
        {
            button.onClick.AddListener(panel.Open);
        }
    }
}