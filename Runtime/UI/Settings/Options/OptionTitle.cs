using Core.Utilities.Extensions;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace Core.UI.Settings.Options
{
    public class OptionTitle : MonoBehaviour
    {
        public const string LocalizationTable = "Settings";

        [SerializeField] private bool localize;
        [SerializeField] [HideIf("localize")] private TMP_Text titleText;
        [SerializeField] [ShowIf("localize")] private LocalizeStringEvent titleTextLocalized;

        public void SetTitle(string title)
        {
            if (localize)
            {
                titleTextLocalized.StringReference = new LocalizedString(LocalizationTable, title);
            }
            else
            {
                titleText.text = StringExtension.Prettify(title);
            }
        }
    }
}