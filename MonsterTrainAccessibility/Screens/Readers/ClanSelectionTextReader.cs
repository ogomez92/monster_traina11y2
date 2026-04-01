using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens.Readers
{
    /// <summary>
    /// Extracts text from clan/class selection and champion choice screens.
    /// </summary>
    public static class ClanSelectionTextReader
    {
        /// <summary>
        /// Get text for clan selection icons (ClassSelectionIcon component)
        /// </summary>
        public static string GetClanSelectionText(GameObject go)
        {
            try
            {
                // Look for ClassSelectionIcon component on this object or parents
                Component classSelectionIcon = null;
                Transform current = go.transform;

                while (current != null && classSelectionIcon == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;

                        if (typeName == "ClassSelectionIcon")
                        {
                            classSelectionIcon = component;
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (classSelectionIcon == null)
                    return null;

                var iconType = classSelectionIcon.GetType();

                // Determine if this is main clan or allied clan selection based on parent names
                bool isMainClan = false;
                bool isAlliedClan = false;
                current = go.transform;
                while (current != null)
                {
                    string parentName = current.name.ToLower();
                    if (parentName.Contains("main class") || parentName.Contains("primary"))
                    {
                        isMainClan = true;
                        break;
                    }
                    if (parentName.Contains("sub class") || parentName.Contains("allied") || parentName.Contains("secondary"))
                    {
                        isAlliedClan = true;
                        break;
                    }
                    current = current.parent;
                }

                // Try to get the ClassData from the component
                object classData = null;

                // Try the 'data' property first (found in log output)
                var dataProp = iconType.GetProperty("data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (dataProp != null)
                {
                    classData = dataProp.GetValue(classSelectionIcon);
                    if (classData != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found clan data via property: data, type: {classData.GetType().Name}");
                    }
                }

                // Try backing field if property didn't work
                if (classData == null)
                {
                    var backingField = iconType.GetField("<data>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (backingField != null)
                    {
                        classData = backingField.GetValue(classSelectionIcon);
                        if (classData != null)
                        {
                            MonsterTrainAccessibility.LogInfo($"Found clan data via backing field, type: {classData.GetType().Name}");
                        }
                    }
                }

                // Try various other field names for the class data
                if (classData == null)
                {
                    var fieldNames = new[] { "classData", "_classData", "linkedClass", "_linkedClass", "ClassData", "_data" };
                    foreach (var fieldName in fieldNames)
                    {
                        var field = iconType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            classData = field.GetValue(classSelectionIcon);
                            if (classData != null)
                            {
                                MonsterTrainAccessibility.LogInfo($"Found clan data via field: {fieldName}");
                                break;
                            }
                        }
                    }
                }

                // Try other properties if still not found
                if (classData == null)
                {
                    var propNames = new[] { "ClassData", "LinkedClass", "GetClassData", "Data" };
                    foreach (var propName in propNames)
                    {
                        var prop = iconType.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (prop != null)
                        {
                            classData = prop.GetValue(classSelectionIcon);
                            if (classData != null)
                            {
                                MonsterTrainAccessibility.LogInfo($"Found clan data via property: {propName}");
                                break;
                            }
                        }
                    }
                }

                if (classData == null)
                {
                    // Log available fields/properties for debugging
                    var fields = iconType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var props = iconType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    MonsterTrainAccessibility.LogInfo($"ClassSelectionIcon fields: {string.Join(", ", fields.Select(f => f.Name))}");
                    MonsterTrainAccessibility.LogInfo($"ClassSelectionIcon properties: {string.Join(", ", props.Select(p => p.Name))}");
                    return "Clan option";
                }

                var classOptionDataType = classData.GetType();

                // The data property returns ClassOptionData which wraps the actual ClassData
                // ClassOptionData has: isRandom, classData, isLocked
                bool isRandom = false;
                bool isLocked = false;
                object actualClassData = classData;

                // Check if this is ClassOptionData (wrapper type)
                if (classOptionDataType.Name == "ClassOptionData")
                {
                    // Get isRandom field
                    var isRandomField = classOptionDataType.GetField("isRandom", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (isRandomField != null)
                    {
                        isRandom = (bool)isRandomField.GetValue(classData);
                    }

                    // Get isLocked field
                    var isLockedField = classOptionDataType.GetField("isLocked", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (isLockedField != null)
                    {
                        isLocked = (bool)isLockedField.GetValue(classData);
                    }

                    // Get the actual classData field
                    var actualClassDataField = classOptionDataType.GetField("classData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (actualClassDataField != null)
                    {
                        actualClassData = actualClassDataField.GetValue(classData);
                    }

                    MonsterTrainAccessibility.LogInfo($"ClassOptionData: isRandom={isRandom}, isLocked={isLocked}, actualClassData={actualClassData?.GetType().Name ?? "null"}");
                }

                // Handle random option
                if (isRandom)
                {
                    if (isMainClan)
                        return "Primary clan: Random. Select a random clan for this run.";
                    else if (isAlliedClan)
                        return "Allied clan: Random. Select a random allied clan for this run.";
                    else
                        return "Random clan option";
                }

                // If we couldn't get the actual class data, return locked message or generic
                if (actualClassData == null)
                {
                    if (isLocked)
                        return isMainClan ? "Primary clan: Locked" : (isAlliedClan ? "Allied clan: Locked" : "Locked clan");
                    return "Clan option";
                }

                var classDataType = actualClassData.GetType();
                MonsterTrainAccessibility.LogInfo($"Actual ClassData type: {classDataType.Name}");

                // Log available methods and fields on the actual classData for debugging
                var classDataMethods = classDataType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object))
                    .Select(m => m.Name)
                    .Distinct()
                    .Take(20);
                var classDataFieldNames = classDataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Select(f => f.Name)
                    .Take(20);
                MonsterTrainAccessibility.LogInfo($"ClassData methods: {string.Join(", ", classDataMethods)}");
                MonsterTrainAccessibility.LogInfo($"ClassData fields: {string.Join(", ", classDataFieldNames)}");

                string clanName = null;
                string clanDescription = null;

                // Get clan name via GetTitle() method
                var getTitleMethod = classDataType.GetMethod("GetTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    clanName = getTitleMethod.Invoke(actualClassData, null) as string;
                    MonsterTrainAccessibility.LogInfo($"GetTitle() returned: {clanName}");
                }

                // Fallback: try titleLoc field with Localize()
                if (string.IsNullOrEmpty(clanName))
                {
                    var titleLocField = classDataType.GetField("titleLoc", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (titleLocField != null)
                    {
                        var titleLoc = titleLocField.GetValue(actualClassData) as string;
                        MonsterTrainAccessibility.LogInfo($"titleLoc field: {titleLoc}");
                        if (!string.IsNullOrEmpty(titleLoc))
                        {
                            clanName = LocalizationHelper.TryLocalize(titleLoc);
                        }
                    }
                }

                // Get description - use GetSubclassDescription for allied clan, GetDescription for main clan
                // These methods return localized text directly
                string descMethodName = isAlliedClan ? "GetSubclassDescription" : "GetDescription";
                var getDescMethod = classDataType.GetMethod(descMethodName, Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    clanDescription = getDescMethod.Invoke(actualClassData, null) as string;
                    MonsterTrainAccessibility.LogInfo($"{descMethodName}() returned: {clanDescription}");
                }

                // Fallback to regular GetDescription if subclass description wasn't found
                if (string.IsNullOrEmpty(clanDescription) && isAlliedClan)
                {
                    var fallbackDescMethod = classDataType.GetMethod("GetDescription", Type.EmptyTypes);
                    if (fallbackDescMethod != null)
                    {
                        clanDescription = fallbackDescMethod.Invoke(actualClassData, null) as string;
                        MonsterTrainAccessibility.LogInfo($"GetDescription() fallback returned: {clanDescription}");
                    }
                }

                // Build the result
                var result = new StringBuilder();

                if (isMainClan)
                {
                    result.Append("Primary clan: ");
                }
                else if (isAlliedClan)
                {
                    result.Append("Allied clan: ");
                }

                if (!string.IsNullOrEmpty(clanName))
                {
                    result.Append(clanName);
                    if (isLocked)
                    {
                        result.Append(" (Locked)");
                    }
                }
                else
                {
                    result.Append(isLocked ? "Locked clan" : "Unknown clan");
                }

                if (!string.IsNullOrEmpty(clanDescription))
                {
                    result.Append(". ");
                    result.Append(TextUtilities.StripRichTextTags(clanDescription));
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting clan selection text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get text for champion choice buttons on the clan selection screen
        /// </summary>
        public static string GetChampionChoiceText(GameObject go)
        {
            try
            {
                // Look for ChampionChoiceButton component
                Component championButton = null;
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component != null && component.GetType().Name == "ChampionChoiceButton")
                    {
                        championButton = component;
                        break;
                    }
                }

                if (championButton == null)
                    return null;

                var buttonType = championButton.GetType();

                // Check for locked tooltip key - if this exists and has a value, the champion is locked
                string lockedTooltipKey = null;
                var lockedKeyField = buttonType.GetField("lockedTooltipTitleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (lockedKeyField != null)
                {
                    lockedTooltipKey = lockedKeyField.GetValue(championButton) as string;
                }

                // Try to find ChampionSelectionUI in parent to get champion data
                Component championSelectionUI = null;
                Transform current = go.transform.parent;
                while (current != null && championSelectionUI == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component != null && component.GetType().Name == "ChampionSelectionUI")
                        {
                            championSelectionUI = component;
                            break;
                        }
                    }
                    current = current.parent;
                }

                object championData = null;
                bool isLocked = false;

                if (championSelectionUI != null)
                {
                    var uiType = championSelectionUI.GetType();

                    // Check if locked
                    var lockedField = uiType.GetField("locked", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (lockedField != null)
                    {
                        isLocked = (bool)lockedField.GetValue(championSelectionUI);
                    }

                    // Get classData from ChampionSelectionUI
                    var classDataField = uiType.GetField("classData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (classDataField != null)
                    {
                        var classData = classDataField.GetValue(championSelectionUI);
                        if (classData != null)
                        {
                            var classDataType = classData.GetType();

                            // Get champions array from classData
                            var championsField = classDataType.GetField("champions", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (championsField != null)
                            {
                                var champions = championsField.GetValue(classData) as System.Collections.IList;
                                if (champions != null && champions.Count > 0)
                                {
                                    // Get button index from name (e.g., "Champion choice button 1" -> index 0)
                                    int buttonIndex = 0;
                                    string buttonName = go.name;
                                    if (buttonName.Contains("1")) buttonIndex = 0;
                                    else if (buttonName.Contains("2")) buttonIndex = 1;

                                    if (buttonIndex < champions.Count)
                                    {
                                        championData = champions[buttonIndex];
                                        MonsterTrainAccessibility.LogInfo($"Found champion at index {buttonIndex}, type: {championData?.GetType().Name}");
                                    }
                                }
                            }
                        }
                    }
                }

                // Try to find tooltip on this object
                string tooltipText = TooltipTextReader.GetTooltipText(go);

                // If we have champion data, try to get the name
                if (championData != null)
                {
                    var champDataType = championData.GetType();

                    // Log methods for debugging
                    var champMethods = champDataType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object))
                        .Select(m => m.Name).Distinct().Take(15);
                    MonsterTrainAccessibility.LogInfo($"ChampionData methods: {string.Join(", ", champMethods)}");

                    // Try GetTitle or GetName methods
                    string championName = null;
                    var getTitleMethod = champDataType.GetMethod("GetTitle", Type.EmptyTypes);
                    if (getTitleMethod != null)
                    {
                        championName = getTitleMethod.Invoke(championData, null) as string;
                    }

                    if (string.IsNullOrEmpty(championName))
                    {
                        var getNameMethod = champDataType.GetMethod("GetName", Type.EmptyTypes);
                        if (getNameMethod != null)
                        {
                            championName = getNameMethod.Invoke(championData, null) as string;
                        }
                    }

                    if (!string.IsNullOrEmpty(championName))
                    {
                        MonsterTrainAccessibility.LogInfo($"Champion name: {championName}, locked: {isLocked}");
                        string result = "Champion: " + championName;
                        if (isLocked)
                            result += " (Locked)";
                        return result;
                    }
                }

                // Use tooltip if available
                if (!string.IsNullOrEmpty(tooltipText))
                {
                    string result = "Champion: " + tooltipText;
                    if (isLocked)
                        result += " (Locked)";
                    return result;
                }

                // Check if locked via tooltip key or isLocked flag
                if (isLocked || !string.IsNullOrEmpty(lockedTooltipKey))
                {
                    if (!string.IsNullOrEmpty(lockedTooltipKey))
                    {
                        string lockedText = LocalizationHelper.TryLocalize(lockedTooltipKey);
                        if (!string.IsNullOrEmpty(lockedText) && lockedText != lockedTooltipKey)
                        {
                            return "Champion: " + lockedText;
                        }
                    }
                    return "Champion: Locked. Win a run to unlock.";
                }

                return "Champion option";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting champion choice text: {ex.Message}");
            }
            return null;
        }

    }
}
