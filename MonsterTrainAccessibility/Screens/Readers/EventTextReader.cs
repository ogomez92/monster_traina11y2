using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens.Readers
{
    /// <summary>
    /// Extracts text from story event choice items and reward descriptions.
    /// </summary>
    public static class EventTextReader
    {
        /// <summary>
        /// Get text from StoryChoiceItem. Reads the choice label + optional reward label,
        /// then looks up reward descriptions via AllGameData for each RewardInfo.
        /// </summary>
        public static string GetStoryChoiceText(GameObject go, Component storyChoiceComponent)
        {
            try
            {
                var compType = storyChoiceComponent.GetType();

                // Read label and optionalRewardLabel directly from StoryChoiceItem fields
                string choiceLabel = ReadTMPField(storyChoiceComponent, compType, "label");
                string rewardLabel = ReadTMPField(storyChoiceComponent, compType, "optionalRewardLabel");

                var sb = new StringBuilder();

                if (!string.IsNullOrEmpty(choiceLabel))
                {
                    sb.Append(TextUtilities.CleanSpriteTagsForSpeech(choiceLabel));
                }

                if (!string.IsNullOrEmpty(rewardLabel))
                {
                    sb.Append($" {TextUtilities.CleanSpriteTagsForSpeech(rewardLabel)}");
                }

                // Try to get reward descriptions from the choiceData → RewardInfo list
                string rewardDescriptions = GetRewardDescriptions(storyChoiceComponent, compType);
                if (!string.IsNullOrEmpty(rewardDescriptions))
                {
                    sb.Append($". {rewardDescriptions}");
                }

                string result = sb.ToString().Trim();
                if (!string.IsNullOrEmpty(result))
                {
                    MonsterTrainAccessibility.LogInfo($"StoryChoiceItem text: {result}");
                    return result;
                }

                // Fallback: scan all visible TMP text in children
                var texts = new List<string>();
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
                                texts.Add(TextUtilities.CleanSpriteTagsForSpeech(text));
                        }
                    }
                }

                if (texts.Count > 0)
                    return string.Join(" ", texts);

                return "Event choice";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting story choice text: {ex.Message}");
                return "Event choice";
            }
        }

        /// <summary>
        /// Read a TMP_Text field by name from a component.
        /// </summary>
        private static string ReadTMPField(Component comp, Type compType, string fieldName)
        {
            try
            {
                var field = compType.GetField(fieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) return null;

                var labelObj = field.GetValue(comp);
                if (labelObj == null) return null;

                // Check if the GameObject is active
                if (labelObj is Component labelComp && !labelComp.gameObject.activeInHierarchy)
                    return null;

                return UITextHelper.GetTextFromComponent(labelObj);
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Look up reward descriptions for each RewardInfo in the choice's data.
        /// Uses AllGameData.FindEnhancerDataByName / FindCollectableRelicDataByName etc.
        /// to get the actual reward object and extract its description.
        /// </summary>
        private static string GetRewardDescriptions(Component storyChoiceItem, Type compType)
        {
            try
            {
                // Get choiceData field (StoryChoiceData)
                var choiceDataField = compType.GetField("choiceData",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (choiceDataField == null) return null;

                var choiceData = choiceDataField.GetValue(storyChoiceItem);
                if (choiceData == null) return null;

                // Call GetRewardsInfo() or GetDisplayableRewardsInfo()
                var cdType = choiceData.GetType();
                var getRewardsMethod = cdType.GetMethod("GetDisplayableRewardsInfo", Type.EmptyTypes)
                                    ?? cdType.GetMethod("GetRewardsInfo", Type.EmptyTypes);
                if (getRewardsMethod == null) return null;

                var rewardsList = getRewardsMethod.Invoke(choiceData, null) as IList;
                if (rewardsList == null || rewardsList.Count == 0) return null;

                // Find SaveManager to access AllGameData
                object saveManager = FindSaveManager();
                if (saveManager == null) return null;

                var smType = saveManager.GetType();
                var getAllGameData = smType.GetMethod("GetAllGameData", Type.EmptyTypes);
                if (getAllGameData == null) return null;

                var allGameData = getAllGameData.Invoke(saveManager, null);
                if (allGameData == null) return null;

                var agdType = allGameData.GetType();
                var descriptions = new List<string>();

                foreach (var rewardInfo in rewardsList)
                {
                    if (rewardInfo == null) continue;
                    var riType = rewardInfo.GetType();

                    var previewTypeField = riType.GetField("previewType");
                    var dataKeyField = riType.GetField("dataKey");
                    if (previewTypeField == null || dataKeyField == null) continue;

                    string previewType = previewTypeField.GetValue(rewardInfo)?.ToString();
                    string dataKey = dataKeyField.GetValue(rewardInfo) as string;
                    if (string.IsNullOrEmpty(dataKey)) continue;

                    string desc = GetRewardDescription(agdType, allGameData, previewType, dataKey);
                    if (!string.IsNullOrEmpty(desc))
                        descriptions.Add(desc);
                }

                return descriptions.Count > 0 ? string.Join(". ", descriptions) : null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"GetRewardDescriptions error: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get a description for a single reward by looking it up in AllGameData.
        /// </summary>
        private static string GetRewardDescription(Type agdType, object allGameData,
            string previewType, string dataKey)
        {
            try
            {
                switch (previewType)
                {
                    case "Upgrade":
                    {
                        var findMethod = agdType.GetMethod("FindEnhancerDataByName",
                            new[] { typeof(string) });
                        var enhancer = findMethod?.Invoke(allGameData, new object[] { dataKey });
                        if (enhancer != null)
                            return GetObjectDescription(enhancer);
                        break;
                    }
                    case "Relic":
                    case "Relic_Name":
                    {
                        var findMethod = agdType.GetMethod("FindCollectableRelicDataByName",
                            new[] { typeof(string) });
                        var relic = findMethod?.Invoke(allGameData, new object[] { dataKey });
                        if (relic != null)
                            return GetObjectDescription(relic);
                        break;
                    }
                    case "Card":
                    {
                        var findMethod = agdType.GetMethod("FindCardDataByName",
                            new[] { typeof(string) });
                        var card = findMethod?.Invoke(allGameData, new object[] { dataKey });
                        if (card != null)
                            return GetObjectDescription(card);
                        break;
                    }
                    case "Reward":
                    {
                        var findMethod = agdType.GetMethod("FindRewardDataByName",
                            new[] { typeof(string) });
                        var reward = findMethod?.Invoke(allGameData, new object[] { dataKey });
                        if (reward != null)
                            return GetObjectDescription(reward);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"GetRewardDescription error for {previewType}:{dataKey}: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Extract description from a game data object via GetDescription() or similar.
        /// </summary>
        private static string GetObjectDescription(object data)
        {
            if (data == null) return null;
            try
            {
                var dataType = data.GetType();
                string desc = null;

                // Try GetDescription
                var getDesc = dataType.GetMethod("GetDescription", Type.EmptyTypes);
                if (getDesc != null)
                {
                    desc = getDesc.Invoke(data, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                        return TextUtilities.CleanSpriteTagsForSpeech(desc);
                }

                // For EnhancerData: try effects[0].GetParamCardUpgradeData() → description
                var getEffects = dataType.GetMethod("GetEffects", Type.EmptyTypes);
                if (getEffects != null)
                {
                    var effects = getEffects.Invoke(data, null) as IList;
                    if (effects != null && effects.Count > 0)
                    {
                        var effect = effects[0];
                        var getUpgrade = effect.GetType().GetMethod("GetParamCardUpgradeData", Type.EmptyTypes);
                        if (getUpgrade != null)
                        {
                            var upgradeData = getUpgrade.Invoke(effect, null);
                            if (upgradeData != null)
                            {
                                string upgradeDesc = CardTextReader.ExtractCardUpgradeDescription(upgradeData);
                                if (!string.IsNullOrEmpty(upgradeDesc))
                                    return upgradeDesc;
                            }
                        }
                    }
                }

                // Try GetDescriptionKey → localize
                var getDescKey = dataType.GetMethod("GetDescriptionKey", Type.EmptyTypes);
                if (getDescKey != null)
                {
                    var key = getDescKey.Invoke(data, null) as string;
                    if (!string.IsNullOrEmpty(key))
                    {
                        desc = LocalizationHelper.Localize(key);
                        if (!string.IsNullOrEmpty(desc))
                            return TextUtilities.CleanSpriteTagsForSpeech(desc);
                    }
                }
            }
            catch { }
            return null;
        }

        private static object FindSaveManager()
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("SaveManager");
                    if (t != null)
                        return UnityEngine.Object.FindObjectOfType(t);
                }
            }
            catch { }
            return null;
        }
    }
}
