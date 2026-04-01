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
    /// Extracts text from relic/artifact info UI elements.
    /// </summary>
    public static class RelicTextReader
    {
        /// <summary>
        /// Get text for RelicInfoUI (artifact selection on RelicDraftScreen)
        /// </summary>
        public static string GetRelicInfoText(GameObject go)
        {
            try
            {
                // Check if this has a RelicInfoUI component
                Component relicInfoUI = null;
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    if (component.GetType().Name == "RelicInfoUI")
                    {
                        relicInfoUI = component;
                        break;
                    }
                }

                if (relicInfoUI == null)
                    return null;

                var relicType = relicInfoUI.GetType();
                MonsterTrainAccessibility.LogInfo($"Found RelicInfoUI, extracting relic data...");

                // Log all fields first to see what's available
                var sbFields = new StringBuilder();
                sbFields.AppendLine($"=== Fields on RelicInfoUI ===");
                foreach (var field in relicType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    try
                    {
                        var value = field.GetValue(relicInfoUI);
                        string valueStr = value?.ToString() ?? "null";
                        if (valueStr.Length > 80) valueStr = valueStr.Substring(0, 80) + "...";
                        sbFields.AppendLine($"  {field.FieldType.Name} {field.Name} = {valueStr}");
                    }
                    catch { }
                }
                MonsterTrainAccessibility.LogInfo(sbFields.ToString());

                // Try to get RelicData from the backing field (C# auto-property)
                string relicName = null;
                string relicDescription = null;

                // Access <relicData>k__BackingField - the backing field for the relicData property
                var backingField = relicType.GetField("<relicData>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField != null)
                {
                    var relicData = backingField.GetValue(relicInfoUI);
                    if (relicData != null)
                    {
                        var dataType = relicData.GetType();
                        MonsterTrainAccessibility.LogInfo($"Found RelicData: {dataType.Name}");

                        // Log available methods to find description
                        var methods = dataType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                        var descMethods = methods.Where(m => m.Name.ToLower().Contains("desc") || m.Name.ToLower().Contains("effect") || m.Name.ToLower().Contains("text")).ToList();
                        MonsterTrainAccessibility.LogInfo($"Potential description methods: {string.Join(", ", descMethods.Select(m => m.Name))}");

                        // Try GetName()
                        var getNameMethod = dataType.GetMethod("GetName", BindingFlags.Public | BindingFlags.Instance);
                        if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                        {
                            relicName = getNameMethod.Invoke(relicData, null) as string;
                            MonsterTrainAccessibility.LogInfo($"GetName() returned: '{relicName}'");
                        }

                        // Try various description method names
                        string[] descMethodNames = { "GetDescription", "GetEffectText", "GetDescriptionText", "GetRelicEffectText", "GetEffectDescription" };
                        foreach (var methodName in descMethodNames)
                        {
                            var method = dataType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                            if (method != null && method.GetParameters().Length == 0)
                            {
                                relicDescription = method.Invoke(relicData, null) as string;
                                if (!string.IsNullOrEmpty(relicDescription))
                                {
                                    MonsterTrainAccessibility.LogInfo($"{methodName}() returned: '{relicDescription}'");
                                    break;
                                }
                            }
                        }

                        // If still no description, try GetDescriptionKey and localize
                        if (string.IsNullOrEmpty(relicDescription))
                        {
                            var descKeyMethod = dataType.GetMethod("GetDescriptionKey", BindingFlags.Public | BindingFlags.Instance);
                            if (descKeyMethod != null && descKeyMethod.GetParameters().Length == 0)
                            {
                                var key = descKeyMethod.Invoke(relicData, null) as string;
                                if (!string.IsNullOrEmpty(key))
                                {
                                    relicDescription = LocalizationHelper.TryLocalize(key);
                                    MonsterTrainAccessibility.LogInfo($"GetDescriptionKey() -> Localized: '{relicDescription}'");
                                }
                            }
                        }
                    }
                }

                // If description looks like a localization key, try getting it from RelicState instead
                if (!string.IsNullOrEmpty(relicDescription) && relicDescription.Contains("_descriptionKey"))
                {
                    MonsterTrainAccessibility.LogInfo("Description is a loc key, trying RelicState...");
                    relicDescription = null; // Clear it, will try relicState
                }

                // Try relicState for description if we don't have one yet
                if (string.IsNullOrEmpty(relicDescription))
                {
                    var relicStateField = relicType.GetField("relicState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (relicStateField != null)
                    {
                        var relicState = relicStateField.GetValue(relicInfoUI);
                        if (relicState != null)
                        {
                            var stateType = relicState.GetType();

                            // Log available methods on RelicState
                            var stateMethods = stateType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                            var descStateMethods = stateMethods.Where(m => m.Name.ToLower().Contains("desc") || m.Name.ToLower().Contains("effect") || m.Name.ToLower().Contains("text")).ToList();
                            MonsterTrainAccessibility.LogInfo($"RelicState methods: {string.Join(", ", descStateMethods.Select(m => m.Name))}");

                            // Try GetDescription on RelicState
                            foreach (var methodName in new[] { "GetDescription", "GetEffectText", "GetDescriptionText" })
                            {
                                var method = stateType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                                if (method != null)
                                {
                                    var parameters = method.GetParameters();
                                    var paramCount = parameters.Length;
                                    MonsterTrainAccessibility.LogInfo($"Found {methodName} with {paramCount} params");

                                    try
                                    {
                                        if (paramCount == 0)
                                        {
                                            relicDescription = method.Invoke(relicState, null) as string;
                                        }
                                        else if (paramCount == 1)
                                        {
                                            // Try calling with null or default value
                                            var paramType = parameters[0].ParameterType;
                                            MonsterTrainAccessibility.LogInfo($"  Param type: {paramType.Name}");

                                            object arg = null;
                                            if (paramType.IsValueType)
                                            {
                                                arg = Activator.CreateInstance(paramType);
                                            }
                                            relicDescription = method.Invoke(relicState, new[] { arg }) as string;
                                        }

                                        MonsterTrainAccessibility.LogInfo($"RelicState.{methodName}() returned: '{relicDescription ?? "null"}'");
                                        if (!string.IsNullOrEmpty(relicDescription) && !relicDescription.Contains("_descriptionKey"))
                                        {
                                            break;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        MonsterTrainAccessibility.LogError($"Error calling {methodName}: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                }

                // Build result
                if (!string.IsNullOrEmpty(relicName))
                {
                    var sb = new StringBuilder();
                    sb.Append("Artifact: ");
                    sb.Append(relicName);

                    if (!string.IsNullOrEmpty(relicDescription) && !relicDescription.Contains("_descriptionKey"))
                    {
                        // Clean up sprite tags like <sprite name=Gold> -> "gold"
                        string cleanDesc = TextUtilities.CleanSpriteTagsForSpeech(relicDescription);
                        sb.Append(". ");
                        sb.Append(cleanDesc);

                        // Extract and append keyword explanations
                        var keywords = new List<string>();
                        CardTextReader.ExtractKeywordsFromDescription(relicDescription, keywords);
                        if (keywords.Count > 0)
                        {
                            sb.Append(" Keywords: ");
                            sb.Append(string.Join(". ", keywords));
                            sb.Append(".");
                        }
                    }

                    string result = sb.ToString();
                    MonsterTrainAccessibility.LogInfo($"Relic text: {result}");
                    return result;
                }

                MonsterTrainAccessibility.LogInfo("Could not extract relic info");
                return null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting relic info text: {ex.Message}");
            }

            return null;
        }

    }
}
