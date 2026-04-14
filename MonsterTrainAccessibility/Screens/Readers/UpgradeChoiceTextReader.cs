using MonsterTrainAccessibility.Utilities;
using System;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens.Readers
{
    /// <summary>
    /// Extracts text for champion upgrade choice tiles (UpgradeCardChoiceItem on
    /// the ChampionUpgradeScreen). Reads the upgrade's localized title and
    /// description, plus the resulting upgraded card's details.
    /// </summary>
    public static class UpgradeChoiceTextReader
    {
        public static string GetUpgradeChoiceText(GameObject go)
        {
            try
            {
                Component itemUi = null;
                Transform current = go.transform;
                while (current != null && itemUi == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "UpgradeCardChoiceItem")
                        {
                            itemUi = component;
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (itemUi == null) return null;

                var itemType = itemUi.GetType();
                var upgradeDataProp = itemType.GetProperty("upgradeData",
                    BindingFlags.Public | BindingFlags.Instance);
                object upgradeData = upgradeDataProp?.GetValue(itemUi);
                if (upgradeData == null) return null;

                var udType = upgradeData.GetType();

                object upgradeState = udType.GetField("upgradeState",
                    BindingFlags.Public | BindingFlags.Instance)?.GetValue(upgradeData);

                object postCardState = udType.GetField("postCardState",
                    BindingFlags.Public | BindingFlags.Instance)?.GetValue(upgradeData);

                string title = null;
                string description = null;
                if (upgradeState != null)
                {
                    var usType = upgradeState.GetType();
                    var titleKey = usType.GetMethod("GetUpgradeTitleKey", Type.EmptyTypes)
                        ?.Invoke(upgradeState, null) as string;
                    var descKey = usType.GetMethod("GetUpgradeDescriptionKey", Type.EmptyTypes)
                        ?.Invoke(upgradeState, null) as string;

                    if (!string.IsNullOrEmpty(titleKey))
                        title = LocalizationHelper.Localize(titleKey);
                    if (!string.IsNullOrEmpty(descKey))
                        description = LocalizationHelper.Localize(descKey);
                }

                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(title))
                    sb.Append(TextUtilities.CleanSpriteTagsForSpeech(title));
                else
                    sb.Append("Upgrade");

                if (!string.IsNullOrEmpty(description))
                {
                    var cleanedDesc = TextUtilities.CleanSpriteTagsForSpeech(description);
                    cleanedDesc = TextUtilities.TrimTrailingPunctuation(cleanedDesc);
                    if (!string.IsNullOrEmpty(cleanedDesc))
                        sb.Append(". ").Append(cleanedDesc);
                }

                if (postCardState != null)
                {
                    var cardDetails = CardTextReader.FormatCardDetails(postCardState);
                    if (!string.IsNullOrEmpty(cardDetails))
                        sb.Append(". ").Append(cardDetails);
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"UpgradeChoiceTextReader error: {ex.Message}");
                return null;
            }
        }
    }
}
