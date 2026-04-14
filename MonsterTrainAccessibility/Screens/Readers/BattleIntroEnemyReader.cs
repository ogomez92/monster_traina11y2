using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens.Readers
{
    /// <summary>
    /// Reads enemy preview tooltips on the BattleIntroScreen. The enemy silhouette's
    /// GameObject carries a BattleIntroEnemyBounds component plus a TooltipProviderComponent
    /// populated via TooltipGenerator.GetCharacterPreviewTooltips — a structured list of
    /// TooltipContent {title, body} covering the name/stats followed by keyword tooltips.
    /// </summary>
    public static class BattleIntroEnemyReader
    {
        public static string GetEnemyPreviewText(GameObject go)
        {
            if (go == null) return null;
            try
            {
                Component tooltipProvider = null;
                bool isEnemyBounds = false;

                Transform current = go.transform;
                while (current != null && (tooltipProvider == null || !isEnemyBounds))
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;
                        if (typeName == "BattleIntroEnemyBounds")
                            isEnemyBounds = true;
                        if (tooltipProvider == null && typeName == "TooltipProviderComponent")
                            tooltipProvider = component;
                    }
                    current = current.parent;
                }

                if (!isEnemyBounds || tooltipProvider == null) return null;

                var providerType = tooltipProvider.GetType();
                IList tooltipList = null;
                var tooltipsProp = providerType.GetProperty("Tooltips", BindingFlags.Public | BindingFlags.Instance);
                if (tooltipsProp != null)
                    tooltipList = tooltipsProp.GetValue(tooltipProvider) as IList;
                if (tooltipList == null)
                {
                    var tooltipsField = providerType.GetField("_tooltips", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    tooltipList = tooltipsField?.GetValue(tooltipProvider) as IList;
                }
                if (tooltipList == null || tooltipList.Count == 0) return null;

                var sb = new StringBuilder();
                bool first = true;
                foreach (var tooltip in tooltipList)
                {
                    if (tooltip == null) continue;
                    var tt = tooltip.GetType();
                    string title = tt.GetField("title")?.GetValue(tooltip) as string;
                    string body = tt.GetField("body")?.GetValue(tooltip) as string;

                    title = Clean(title);
                    body = Clean(body);
                    if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body)) continue;

                    if (!first) sb.Append(". ");
                    first = false;

                    if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(body))
                    {
                        sb.Append(title).Append(": ").Append(body);
                    }
                    else
                    {
                        sb.Append(string.IsNullOrEmpty(title) ? body : title);
                    }
                }

                string result = sb.ToString().Trim();
                return string.IsNullOrEmpty(result) ? null : result;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"BattleIntroEnemyReader error: {ex.Message}");
                return null;
            }
        }

        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            text = TextUtilities.CleanSpriteTagsForSpeech(text);
            text = TextUtilities.StripRichTextTags(text);
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
    }
}
