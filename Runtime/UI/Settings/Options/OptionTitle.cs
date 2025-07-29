using Core.Utilities.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace Core.UI.Settings.Options
{
    public class OptionTitle : MonoBehaviour
    {
        public const string LocalizationTable = "Settings";

        [SerializeField] private TMP_Text titleText;
        [SerializeField] private LocalizeStringEvent titleTextLocalized;

        private string title;

        public void SetTitle(string title)
        {
            this.title = title;
            if (titleTextLocalized.StringReference != null)
            {
                titleTextLocalized.enabled = true;
                titleTextLocalized.StringReference.StringChanged -= CheckLocalizedString;
            }

            titleTextLocalized.StringReference = new LocalizedString(LocalizationTable, title);
            titleTextLocalized.StringReference.StringChanged += CheckLocalizedString;
        }

        private void CheckLocalizedString(string value)
        {
            if (!value.Contains("No translation found for")) return;
            titleTextLocalized.enabled = false;
            titleText.text = StringExtension.Prettify(title);
        }
    }
}