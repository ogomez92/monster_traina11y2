using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens.Readers
{
    /// <summary>
    /// Extracts text from story event choice items.
    /// </summary>
    public static class EventTextReader
    {
        /// <summary>
        /// Get text from StoryChoiceItem (random event choices like "The Doors. (Get Trap Chute.)")
        /// </summary>
        public static string GetStoryChoiceText(GameObject go, Component storyChoiceComponent)
        {
            try
            {
                var texts = new List<string>();

                // Look for TMP text components in children
                foreach (var component in go.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    string typeName = component.GetType().Name;
                    if (typeName.Contains("TMP_Text") || typeName.Contains("TextMeshPro"))
                    {
                        if (!component.gameObject.activeInHierarchy) continue;
                        var textProp = component.GetType().GetProperty("text");
                        if (textProp != null)
                        {
                            string text = textProp.GetValue(component) as string;
                            text = text?.Trim();
                            if (!string.IsNullOrEmpty(text) && !TextUtilities.IsPlaceholderText(text))
                            {
                                texts.Add(text);
                            }
                        }
                    }
                }

                if (texts.Count > 0)
                {
                    string result = string.Join(" ", texts);
                    MonsterTrainAccessibility.LogInfo($"StoryChoiceItem text: {result}");
                    return result;
                }

                // Try reflection to get choice data from the component
                var componentType = storyChoiceComponent.GetType();

                // Try to get title/name
                var getTitleMethod = componentType.GetMethod("GetTitle") ??
                                     componentType.GetMethod("GetName") ??
                                     componentType.GetMethod("GetChoiceTitle");
                if (getTitleMethod != null)
                {
                    var title = getTitleMethod.Invoke(storyChoiceComponent, null) as string;
                    if (!string.IsNullOrEmpty(title))
                    {
                        texts.Add(title);
                    }
                }

                // Try to get description
                var getDescMethod = componentType.GetMethod("GetDescription") ??
                                    componentType.GetMethod("GetResultDescription");
                if (getDescMethod != null)
                {
                    var desc = getDescMethod.Invoke(storyChoiceComponent, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                    {
                        texts.Add($"({desc})");
                    }
                }

                if (texts.Count > 0)
                {
                    return string.Join(" ", texts);
                }

                // Log available fields for debugging
                var fields = componentType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var fieldNames = fields.Select(f => f.Name).Take(20);
                MonsterTrainAccessibility.LogInfo($"StoryChoiceItem fields: {string.Join(", ", fieldNames)}");

                return "Event choice";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting story choice text: {ex.Message}");
                return "Event choice";
            }
        }
    }
}
