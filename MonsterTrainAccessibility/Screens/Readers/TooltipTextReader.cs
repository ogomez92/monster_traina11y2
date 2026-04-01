using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterTrainAccessibility.Screens.Readers
{
    /// <summary>
    /// Extracts text from tooltip provider components.
    /// </summary>
    public static class TooltipTextReader
    {
        /// <summary>
        /// Try to get tooltip text from a GameObject's tooltip components
        /// </summary>
        public static string GetTooltipText(GameObject go)
        {
            try
            {
                Transform current = go.transform;
                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        var type = component.GetType();
                        string typeName = type.Name;

                        // Look for TooltipProviderComponent specifically (Monster Train's tooltip system)
                        if (typeName == "TooltipProviderComponent" || typeName.Contains("TooltipProvider"))
                        {
                            string tooltipTitle = GetTooltipProviderTitle(component, type);
                            if (!string.IsNullOrEmpty(tooltipTitle))
                            {
                                MonsterTrainAccessibility.LogInfo($"Found tooltip title: {tooltipTitle}");
                                return tooltipTitle;
                            }
                        }

                        // Look for other tooltip-related components
                        if (typeName.Contains("Tooltip") || typeName.Contains("TooltipDisplay"))
                        {
                            // Try to get tooltip title/text
                            var titleField = type.GetField("titleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (titleField == null)
                                titleField = type.GetField("_titleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (titleField == null)
                                titleField = type.GetField("tooltipTitleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                            if (titleField != null)
                            {
                                string titleKey = titleField.GetValue(component) as string;
                                if (!string.IsNullOrEmpty(titleKey))
                                {
                                    string localized = LocalizationHelper.Localize(titleKey);
                                    if (!string.IsNullOrEmpty(localized))
                                        return localized;
                                }
                            }

                            // Try GetTitle method
                            var getTitleMethod = type.GetMethod("GetTitle", BindingFlags.Public | BindingFlags.Instance);
                            if (getTitleMethod != null && getTitleMethod.GetParameters().Length == 0)
                            {
                                var result = getTitleMethod.Invoke(component, null);
                                if (result is string title && !string.IsNullOrEmpty(title))
                                    return title;
                            }
                        }

                        // Look for scenario/battle data reference
                        if (typeName.Contains("Scenario") || typeName.Contains("Battle"))
                        {
                            // Try GetName method
                            var getNameMethod = type.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                            if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                            {
                                var result = getNameMethod.Invoke(component, null);
                                if (result is string name && !string.IsNullOrEmpty(name))
                                    return "Battle: " + name;
                            }
                        }
                    }
                    current = current.parent;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting tooltip text: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get tooltip text including body/description
        /// </summary>
        public static string GetTooltipTextWithBody(GameObject go)
        {
            string title, body;
            GetTooltipTitleAndBody(go, out title, out body);

            if (!string.IsNullOrEmpty(title))
            {
                if (!string.IsNullOrEmpty(body))
                {
                    return $"{title}. {body}";
                }
                return title;
            }

            return null;
        }

        /// <summary>
        /// Get both title and body from tooltip
        /// </summary>
        public static void GetTooltipTitleAndBody(GameObject go, out string title, out string body)
        {
            title = null;
            body = null;

            try
            {
                Transform current = go.transform;
                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "TooltipProviderComponent")
                        {
                            var type = component.GetType();
                            var tooltipsField = type.GetField("_tooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (tooltipsField != null)
                            {
                                var tooltipsList = tooltipsField.GetValue(component) as System.Collections.IList;
                                if (tooltipsList != null && tooltipsList.Count > 0)
                                {
                                    var tooltip = tooltipsList[0];
                                    var tooltipType = tooltip.GetType();

                                    // Get title and localize if it's a key
                                    var titleField = tooltipType.GetField("title", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (titleField != null)
                                    {
                                        string rawTitle = titleField.GetValue(tooltip) as string;
                                        if (!string.IsNullOrEmpty(rawTitle))
                                        {
                                            // Try to localize - if it looks like a key
                                            title = LocalizationHelper.TryLocalize(rawTitle);
                                            if (string.IsNullOrEmpty(title))
                                                title = rawTitle;
                                        }
                                    }

                                    // Get body and localize if it's a key
                                    var bodyField = tooltipType.GetField("body", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (bodyField != null)
                                    {
                                        string rawBody = bodyField.GetValue(tooltip) as string;
                                        if (!string.IsNullOrEmpty(rawBody))
                                        {
                                            // Try to localize - if it looks like a key
                                            body = LocalizationHelper.TryLocalize(rawBody);
                                            if (string.IsNullOrEmpty(body))
                                                body = rawBody;
                                        }
                                    }

                                    return;
                                }
                            }
                        }
                    }
                    current = current.parent;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting tooltip title and body: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract title from TooltipProviderComponent or LocalizedTooltipProvider
        /// </summary>
        public static string GetTooltipProviderTitle(Component tooltipProvider, Type type)
        {
            try
            {
                string typeName = type.Name;

                // Handle LocalizedTooltipProvider specifically
                if (typeName == "LocalizedTooltipProvider")
                {
                    // Try to get titleKey field
                    var titleKeyField = type.GetField("titleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                     ?? type.GetField("_titleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                     ?? type.GetField("tooltipTitleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (titleKeyField != null)
                    {
                        string titleKey = titleKeyField.GetValue(tooltipProvider) as string;
                        if (!string.IsNullOrEmpty(titleKey))
                        {
                            string localized = LocalizationHelper.TryLocalize(titleKey);
                            if (!string.IsNullOrEmpty(localized))
                            {
                                MonsterTrainAccessibility.LogInfo($"LocalizedTooltipProvider title: {localized}");
                                return localized;
                            }
                        }
                    }

                    // Log fields for debugging
                    var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    MonsterTrainAccessibility.LogInfo($"LocalizedTooltipProvider fields: {string.Join(", ", fields.Select(f => f.Name))}");
                }

                // The TooltipProviderComponent has a _tooltips field which is a list of tooltip data
                var tooltipsField = type.GetField("_tooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (tooltipsField == null)
                {
                    // Try the property
                    var tooltipsProp = type.GetProperty("tooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (tooltipsProp != null)
                    {
                        var tooltipsList = tooltipsProp.GetValue(tooltipProvider) as System.Collections.IList;
                        if (tooltipsList != null && tooltipsList.Count > 0)
                        {
                            return ExtractTitleFromTooltip(tooltipsList[0]);
                        }
                    }
                }
                else
                {
                    var tooltipsList = tooltipsField.GetValue(tooltipProvider) as System.Collections.IList;
                    if (tooltipsList != null && tooltipsList.Count > 0)
                    {
                        return ExtractTitleFromTooltip(tooltipsList[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting tooltip provider title: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract the title from a tooltip data object
        /// </summary>
        public static string ExtractTitleFromTooltip(object tooltip)
        {
            if (tooltip == null) return null;

            try
            {
                var tooltipType = tooltip.GetType();
                MonsterTrainAccessibility.LogInfo($"Tooltip type: {tooltipType.Name}");

                // Log fields for debugging
                var fields = tooltipType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MonsterTrainAccessibility.LogInfo($"Tooltip fields: {string.Join(", ", fields.Select(f => f.Name))}");

                // Try GetTitle method first
                var getTitleMethod = tooltipType.GetMethod("GetTitle", BindingFlags.Public | BindingFlags.Instance);
                if (getTitleMethod != null && getTitleMethod.GetParameters().Length == 0)
                {
                    var result = getTitleMethod.Invoke(tooltip, null);
                    if (result is string title && !string.IsNullOrEmpty(title))
                    {
                        MonsterTrainAccessibility.LogInfo($"Got title from GetTitle(): {title}");
                        return title;
                    }
                }

                // Try common title field names
                string[] titleFieldNames = { "title", "_title", "titleKey", "_titleKey", "tooltipTitleKey", "_tooltipTitleKey", "name", "_name" };
                foreach (var fieldName in titleFieldNames)
                {
                    var field = tooltipType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var value = field.GetValue(tooltip);
                        if (value is string str && !string.IsNullOrEmpty(str))
                        {
                            MonsterTrainAccessibility.LogInfo($"Found title in field {fieldName}: {str}");
                            // Try to localize if it looks like a key
                            string localized = LocalizationHelper.Localize(str);
                            if (!string.IsNullOrEmpty(localized) && localized != str)
                            {
                                MonsterTrainAccessibility.LogInfo($"Localized to: {localized}");
                                return localized;
                            }
                            return str;
                        }
                    }
                }

                // Try title properties
                string[] titlePropNames = { "Title", "TitleKey", "Name" };
                foreach (var propName in titlePropNames)
                {
                    var prop = tooltipType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null)
                    {
                        var value = prop.GetValue(tooltip);
                        if (value is string str && !string.IsNullOrEmpty(str))
                        {
                            MonsterTrainAccessibility.LogInfo($"Found title in property {propName}: {str}");
                            string localized = LocalizationHelper.Localize(str);
                            if (!string.IsNullOrEmpty(localized) && localized != str)
                                return localized;
                            return str;
                        }
                    }
                }

                // Check if it has a nested data object (like CharacterData, ScenarioData)
                string[] dataFieldNames = { "data", "_data", "characterData", "_characterData", "scenarioData", "_scenarioData" };
                foreach (var fieldName in dataFieldNames)
                {
                    var field = tooltipType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var dataObj = field.GetValue(tooltip);
                        if (dataObj != null)
                        {
                            string name = GetNameFromDataObject(dataObj);
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting title from tooltip: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get name from a game data object (CharacterData, ScenarioData, etc.)
        /// </summary>
        public static string GetNameFromDataObject(object dataObj)
        {
            if (dataObj == null) return null;

            try
            {
                var dataType = dataObj.GetType();
                MonsterTrainAccessibility.LogInfo($"Data object type: {dataType.Name}");

                // Try GetName method
                var getNameMethod = dataType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                {
                    var result = getNameMethod.Invoke(dataObj, null);
                    if (result is string name && !string.IsNullOrEmpty(name))
                        return name;
                }

                // Try GetNameKey for localized names
                var getNameKeyMethod = dataType.GetMethod("GetNameKey", BindingFlags.Public | BindingFlags.Instance);
                if (getNameKeyMethod != null && getNameKeyMethod.GetParameters().Length == 0)
                {
                    var result = getNameKeyMethod.Invoke(dataObj, null);
                    if (result is string key && !string.IsNullOrEmpty(key))
                    {
                        string localized = LocalizationHelper.Localize(key);
                        return !string.IsNullOrEmpty(localized) ? localized : key;
                    }
                }

                // Try name fields
                string[] nameFields = { "name", "_name", "nameKey", "_nameKey" };
                foreach (var fieldName in nameFields)
                {
                    var field = dataType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        var value = field.GetValue(dataObj);
                        if (value is string str && !string.IsNullOrEmpty(str))
                        {
                            string localized = LocalizationHelper.Localize(str);
                            return !string.IsNullOrEmpty(localized) ? localized : str;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting name from data object: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract info from a tooltip provider component
        /// </summary>
        public static string ExtractTooltipProviderInfo(object provider)
        {
            if (provider == null) return null;

            try
            {
                var providerType = provider.GetType();

                // Try GetTitle/GetDescription methods
                var getTitleMethod = providerType.GetMethod("GetTitle", Type.EmptyTypes) ??
                                    providerType.GetMethod("GetTooltipTitle", Type.EmptyTypes);
                var getDescMethod = providerType.GetMethod("GetDescription", Type.EmptyTypes) ??
                                   providerType.GetMethod("GetTooltipDescription", Type.EmptyTypes) ??
                                   providerType.GetMethod("GetTooltipBody", Type.EmptyTypes);

                string title = null;
                string desc = null;

                if (getTitleMethod != null)
                {
                    title = getTitleMethod.Invoke(provider, null) as string;
                }
                if (getDescMethod != null)
                {
                    desc = getDescMethod.Invoke(provider, null) as string;
                }

                // Also try fields
                var providerFields = providerType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (string.IsNullOrEmpty(title))
                {
                    var titleField = providerFields.FirstOrDefault(f => f.Name.ToLower().Contains("title"));
                    if (titleField != null)
                        title = titleField.GetValue(provider) as string;
                }
                if (string.IsNullOrEmpty(desc))
                {
                    var descField = providerFields.FirstOrDefault(f =>
                        f.Name.ToLower().Contains("desc") || f.Name.ToLower().Contains("content") || f.Name.ToLower().Contains("body"));
                    if (descField != null)
                        desc = descField.GetValue(provider) as string;
                }

                List<string> parts = new List<string>();
                if (!string.IsNullOrEmpty(title))
                    parts.Add(LocalizationHelper.TryLocalize(title));
                if (!string.IsNullOrEmpty(desc) && desc != title)
                    parts.Add(LocalizationHelper.TryLocalize(desc));

                if (parts.Count > 0)
                {
                    return TextUtilities.StripRichTextTags(string.Join(". ", parts));
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Get text for buttons with LocalizedTooltipProvider (mutator options, challenges, etc.)
        /// </summary>
        public static string GetLocalizedTooltipButtonText(GameObject go)
        {
            try
            {
                // Check if this button has LocalizedTooltipProvider
                Component tooltipProvider = null;
                bool hasButtonToggle = false;

                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    string typeName = component.GetType().Name;

                    if (typeName == "LocalizedTooltipProvider")
                    {
                        tooltipProvider = component;
                    }
                    if (typeName == "ButtonStateBehaviourToggle")
                    {
                        hasButtonToggle = true;
                    }
                }

                // Only handle if we have LocalizedTooltipProvider
                if (tooltipProvider == null)
                    return null;

                var type = tooltipProvider.GetType();

                // Log fields for debugging
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MonsterTrainAccessibility.LogInfo($"LocalizedTooltipProvider fields: {string.Join(", ", fields.Select(f => f.Name))}");

                // Try to get the tooltip title
                string tooltipTitle = null;
                string tooltipBody = null;

                // Try various field names for title
                var titleFieldNames = new[] { "titleKey", "_titleKey", "tooltipTitleKey", "title", "_title" };
                foreach (var fieldName in titleFieldNames)
                {
                    var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        string titleKey = field.GetValue(tooltipProvider) as string;
                        if (!string.IsNullOrEmpty(titleKey))
                        {
                            tooltipTitle = LocalizationHelper.TryLocalize(titleKey);
                            MonsterTrainAccessibility.LogInfo($"Found tooltip title key: {titleKey} -> {tooltipTitle}");
                            break;
                        }
                    }
                }

                // Try various field names for body
                var bodyFieldNames = new[] { "bodyKey", "_bodyKey", "tooltipBodyKey", "body", "_body", "descriptionKey" };
                foreach (var fieldName in bodyFieldNames)
                {
                    var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        string bodyKey = field.GetValue(tooltipProvider) as string;
                        if (!string.IsNullOrEmpty(bodyKey))
                        {
                            tooltipBody = LocalizationHelper.TryLocalize(bodyKey);
                            MonsterTrainAccessibility.LogInfo($"Found tooltip body key: {bodyKey} -> {tooltipBody}");
                            break;
                        }
                    }
                }

                // Build result from button name and tooltip
                var result = new StringBuilder();

                // Use clean button name
                string buttonName = CleanGameObjectName(go.name);
                if (!string.IsNullOrEmpty(buttonName))
                {
                    result.Append(buttonName);
                }

                // Add tooltip title if different from button name
                if (!string.IsNullOrEmpty(tooltipTitle) && tooltipTitle != buttonName)
                {
                    if (result.Length > 0)
                        result.Append(": ");
                    result.Append(tooltipTitle);
                }

                // Add tooltip body
                if (!string.IsNullOrEmpty(tooltipBody))
                {
                    if (result.Length > 0)
                        result.Append(". ");
                    result.Append(TextUtilities.StripRichTextTags(tooltipBody));
                }

                // Check if button shows locked state
                if (hasButtonToggle)
                {
                    // Check interactable state
                    var button = go.GetComponent<Button>();
                    if (button != null && !button.interactable)
                    {
                        result.Append(" (Locked)");
                    }
                }

                if (result.Length > 0)
                    return result.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting localized tooltip button text: {ex.Message}");
            }
            return null;
        }

        private static string CleanGameObjectName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            name = name.Replace("(Clone)", "");
            name = name.Replace("Button", "");
            name = name.Replace("Btn", "");
            name = name.Trim();
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            return name;
        }
    }
}
