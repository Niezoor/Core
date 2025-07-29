using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Settings.Options
{
    public class OptionButton : MonoBehaviour
    {
        [SerializeField] private OptionTitle optionTitle;
        [SerializeField] private Button button;

        public void Setup(MethodInfo methodInfo, object obj)
        {
            optionTitle.SetTitle(methodInfo.Name);
            button.onClick.AddListener(() => methodInfo.Invoke(obj, null));
        }
    }
}