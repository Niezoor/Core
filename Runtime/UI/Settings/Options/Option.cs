using System.Reflection;
using UnityEngine;

namespace Core.UI.Settings.Options
{
    public abstract class Option : MonoBehaviour
    {
        [SerializeField] private OptionTitle optionTitle;

        protected PropertyInfo PropertyInfo;
        protected object Obj;

#if UNITY_EDITOR
        protected virtual void Reset()
        {
            optionTitle = GetComponent<OptionTitle>();
        }
#endif

        public void Setup(PropertyInfo propertyInfo, object obj)
        {
            PropertyInfo = propertyInfo;
            Obj = obj;
            optionTitle.SetTitle(propertyInfo.Name);
            SetupValue(propertyInfo, obj);
        }

        protected abstract void SetupValue(PropertyInfo propertyInfo, object obj);
        public abstract void PrevOption();
        public abstract void NextOption();
        public abstract void Clicked();
    }
}