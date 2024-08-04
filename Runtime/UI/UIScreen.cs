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
        public UIScreenLayer layer;

        private void Awake()
        {
            UIManager.Instance.RegisterScreen(this);
        }
    }
}