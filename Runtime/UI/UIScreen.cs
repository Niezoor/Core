using UnityEngine;

namespace Core.UI
{
    public enum UIScreenLayer
    {
        Top,
        Bottom,
    }

    public class UIScreen : MonoBehaviour
    {
        [SerializeField] private UIScreenLayer layer;

        public UIScreenLayer Layer => layer;

        private void Awake()
        {
            UIManager.Instance.RegisterScreen(this);
        }
    }
}