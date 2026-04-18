using MonsterTrainAccessibility.Utilities;
using System;
using System.Text;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens.Readers
{
    /// <summary>
    /// Extracts text for any screen that hosts SoulSelectionItemUI tiles — Soulforge
    /// plus any other soul-related screen (SoulDraft, SoulSavior variants) that reuses
    /// the same widget and "Soul info content" side panel. The tile itself only carries
    /// the soul name; description and unlock condition live on the side panel which
    /// updates as the user navigates. We read both so a screen-reader user gets the
    /// same information sighted players see.
    /// </summary>
    public static class SoulforgeTextReader
    {
        public static string GetSoulSelectionItemText(GameObject go)
        {
            if (go == null) return null;

            try
            {
                Component itemUi = null;
                Transform current = go.transform;
                while (current != null && itemUi == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "SoulSelectionItemUI")
                        {
                            itemUi = component;
                            break;
                        }
                    }
                    current = current.parent;
                }
                if (itemUi == null) return null;

                string tileName = null;
                var soulNameLabel = UITextHelper.FindChildRecursive(itemUi.transform, "Soul name");
                if (soulNameLabel != null) tileName = ReadLabelText(soulNameLabel);

                // Walk up ancestors until one contains a "Soul info content" descendant.
                // This keeps the reader screen-agnostic: Soulforge, SoulDraft, and any
                // other screen reusing the widget all get the same treatment.
                Transform infoContent = null;
                Transform ancestor = itemUi.transform;
                while (ancestor != null && infoContent == null)
                {
                    infoContent = UITextHelper.FindChildRecursive(ancestor, "Soul info content");
                    ancestor = ancestor.parent;
                }

                string sideName = null, sideDescription = null, unlockDescription = null, unlockNumeric = null;
                if (infoContent != null)
                {
                    sideName = ReadLabelText(UITextHelper.FindChildRecursive(infoContent, "Text soul name"));
                    sideDescription = ReadLabelText(UITextHelper.FindChildRecursive(infoContent, "Text soul description"));

                    var progression = UITextHelper.FindChildRecursive(infoContent, "ProgressionObjective");
                    if (progression != null)
                    {
                        unlockDescription = ReadLabelText(UITextHelper.FindChildRecursive(progression, "Description Label"));
                        unlockNumeric = ReadLabelText(UITextHelper.FindChildRecursive(progression, "Numeric Label"));
                    }
                }

                if (string.IsNullOrEmpty(tileName) && string.IsNullOrEmpty(sideName)) return null;

                string name = !string.IsNullOrEmpty(tileName) ? tileName : sideName;

                // Only trust the side panel's extra detail when it's reflecting the same
                // soul as the focused tile — otherwise we'd announce stale info during
                // the frame after navigation but before the side panel refreshes.
                bool sidePanelMatches = !string.IsNullOrEmpty(tileName)
                    && !string.IsNullOrEmpty(sideName)
                    && string.Equals(tileName.Trim(), sideName.Trim(), StringComparison.OrdinalIgnoreCase);

                var sb = new StringBuilder();
                sb.Append("Soul: ");
                sb.Append(TextUtilities.StripRichTextTags(name));

                if (sidePanelMatches && !string.IsNullOrEmpty(sideDescription))
                {
                    sb.Append(". ");
                    sb.Append(TextUtilities.StripRichTextTags(
                        TextUtilities.CleanSpriteTagsForSpeech(sideDescription)));
                }

                if (sidePanelMatches && !string.IsNullOrEmpty(unlockDescription))
                {
                    sb.Append(". Locked: ");
                    sb.Append(TextUtilities.StripRichTextTags(
                        TextUtilities.CleanSpriteTagsForSpeech(unlockDescription)));

                    if (!string.IsNullOrEmpty(unlockNumeric) && !IsProgressComplete(unlockNumeric))
                    {
                        sb.Append(" (");
                        sb.Append(unlockNumeric);
                        sb.Append(')');
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting soul selection item text: {ex.Message}");
            }
            return null;
        }

        private static bool IsProgressComplete(string numeric)
        {
            if (string.IsNullOrEmpty(numeric)) return false;
            var parts = numeric.Split('/');
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0].Trim(), out int cur)) return false;
            if (!int.TryParse(parts[1].Trim(), out int max)) return false;
            return max > 0 && cur >= max;
        }

        private static string ReadLabelText(Transform t)
        {
            if (t == null) return null;
            foreach (var component in t.GetComponents<Component>())
            {
                if (component == null) continue;
                string typeName = component.GetType().Name;
                if (typeName.Contains("Text") || typeName.Contains("TMP"))
                {
                    var text = UITextHelper.GetTextFromComponent(component);
                    if (!string.IsNullOrEmpty(text)) return text;
                }
            }
            return null;
        }
    }
}
