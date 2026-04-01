using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterTrainAccessibility.Screens.Readers
{
    /// <summary>
    /// Extracts text from compendium/logbook items.
    /// </summary>
    public static class CompendiumTextReader
    {
        /// <summary>
        /// Get text for logbook/compendium items
        /// </summary>
        public static string GetLogbookItemText(GameObject go)
        {
            try
            {
                // Check if this or a parent is part of the compendium
                if (!IsInCompendiumContext(go))
                    return null;

                // Look for count labels (format like "25/250" or "X/Y")
                string countText = FindCountLabelText(go);
                string itemName = GetItemNameFromHierarchy(go);

                if (!string.IsNullOrEmpty(countText) && !string.IsNullOrEmpty(itemName))
                {
                    return $"{itemName}: {countText}";
                }
                else if (!string.IsNullOrEmpty(countText))
                {
                    // Try to make the count more readable
                    return FormatCountText(countText);
                }
                else if (!string.IsNullOrEmpty(itemName))
                {
                    return itemName;
                }
            }
            catch { }
            return null;
        }

        private static bool IsInCompendiumContext(GameObject go)
        {
            Transform current = go.transform;
            while (current != null)
            {
                string name = current.name.ToLower();
                if (name.Contains("compendium") || name.Contains("logbook") ||
                    name.Contains("collection") || name.Contains("cardlist"))
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        private static string FindCountLabelText(GameObject go)
        {
            try
            {
                Transform parent = go.transform.parent;
                if (parent == null) return null;

                var allTexts = new List<string>();
                foreach (Transform sibling in parent)
                {
                    string text = GetTMPTextDirect(sibling.gameObject);
                    if (!string.IsNullOrEmpty(text))
                        allTexts.Add(text.Trim());

                    var uiText = sibling.GetComponent<Text>();
                    if (uiText != null && !string.IsNullOrEmpty(uiText.text))
                        allTexts.Add(uiText.text.Trim());

                    foreach (Transform child in sibling)
                    {
                        string childText = GetTMPTextDirect(child.gameObject);
                        if (!string.IsNullOrEmpty(childText))
                            allTexts.Add(childText.Trim());
                    }
                }

                foreach (var text in allTexts)
                {
                    if (Regex.IsMatch(text, @"^\d+/\d+$"))
                    {
                        return text;
                    }
                }

                string number = null;
                string total = null;
                foreach (var text in allTexts)
                {
                    if (Regex.IsMatch(text, @"^\d+$"))
                    {
                        if (number == null)
                            number = text;
                        else if (total == null)
                            total = text;
                    }
                }

                if (number != null && total != null)
                {
                    return $"{number}/{total}";
                }
            }
            catch { }
            return null;
        }

        private static string GetItemNameFromHierarchy(GameObject go)
        {
            try
            {
                Transform current = go.transform;
                for (int i = 0; i < 3 && current != null; i++)
                {
                    if (current.parent != null)
                    {
                        foreach (Transform sibling in current.parent)
                        {
                            string sibName = sibling.name.ToLower();
                            if (sibName.Contains("title") || sibName.Contains("name") ||
                                sibName.Contains("label") || sibName.Contains("header"))
                            {
                                string text = GetTMPTextDirect(sibling.gameObject);
                                if (string.IsNullOrEmpty(text))
                                {
                                    var uiText = sibling.GetComponent<Text>();
                                    text = uiText?.text;
                                }
                                if (!string.IsNullOrEmpty(text) && text.Length > 2)
                                {
                                    if (!Regex.IsMatch(text.Trim(), @"^\d+$"))
                                    {
                                        return text.Trim();
                                    }
                                }
                            }
                        }
                    }
                    current = current.parent;
                }
            }
            catch { }
            return null;
        }

        private static string FormatCountText(string countText)
        {
            if (string.IsNullOrEmpty(countText))
                return null;

            var match = Regex.Match(countText, @"^(\d+)/(\d+)$");
            if (match.Success)
            {
                return $"{match.Groups[1].Value} of {match.Groups[2].Value} discovered";
            }

            return countText;
        }

        private static string GetTMPTextDirect(GameObject go)
        {
            try
            {
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                    {
                        var textProperty = type.GetProperty("text");
                        if (textProperty != null)
                        {
                            return textProperty.GetValue(component) as string;
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
