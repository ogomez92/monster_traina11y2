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
    /// Extracts text from shop/merchant items and services.
    /// </summary>
    public static class ShopTextReader
    {
        /// <summary>
        /// Get text for shop items (cards, relics, services/upgrades)
        /// </summary>
        public static string GetShopItemText(GameObject go)
        {
            try
            {
                // Look for MerchantGoodDetailsUI (cards/relics for sale)
                Component goodDetailsUI = FindComponentInHierarchy(go, "MerchantGoodDetailsUI");
                if (goodDetailsUI != null)
                {
                    string goodText = ExtractMerchantGoodInfo(goodDetailsUI);
                    if (!string.IsNullOrEmpty(goodText))
                    {
                        return goodText;
                    }
                }

                // Look for MerchantServiceUI (services/upgrades)
                Component serviceUI = FindComponentInHierarchy(go, "MerchantServiceUI");
                if (serviceUI != null)
                {
                    string serviceText = ExtractMerchantServiceInfo(serviceUI);
                    if (!string.IsNullOrEmpty(serviceText))
                    {
                        return serviceText;
                    }
                }

                // Look for BuyButton component to get price
                Component buyButton = FindComponentInHierarchy(go, "BuyButton");
                if (buyButton != null)
                {
                    // Try to find the associated good or service
                    var buyType = buyButton.GetType();
                    var buyFields = buyType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    // Look for good/service reference
                    foreach (var field in buyFields)
                    {
                        var value = field.GetValue(buyButton);
                        if (value == null) continue;

                        string typeName = value.GetType().Name;
                        if (typeName.Contains("Good") || typeName.Contains("Service") ||
                            typeName.Contains("Card") || typeName.Contains("Relic"))
                        {
                            string info = ExtractShopItemInfo(value);
                            if (!string.IsNullOrEmpty(info))
                            {
                                return info;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting shop item text: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get price from a shop item component
        /// </summary>
        public static string GetShopItemPrice(Component shopItem)
        {
            try
            {
                var itemType = shopItem.GetType();
                var fields = itemType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (var field in fields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("price") || fieldName.Contains("cost") || fieldName.Contains("gold"))
                    {
                        var value = field.GetValue(shopItem);
                        if (value is int intPrice && intPrice > 0)
                        {
                            return $"{intPrice} gold";
                        }
                    }
                }

                // Try methods
                var methods = itemType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                var getPriceMethod = methods.FirstOrDefault(m =>
                    (m.Name == "GetPrice" || m.Name == "GetCost" || m.Name == "GetGoldCost") &&
                    m.GetParameters().Length == 0);

                if (getPriceMethod != null)
                {
                    var result = getPriceMethod.Invoke(shopItem, null);
                    if (result is int price && price > 0)
                    {
                        return $"{price} gold";
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Extract info from a generic shop item
        /// </summary>
        public static string ExtractShopItemInfo(object item)
        {
            if (item == null) return null;

            try
            {
                var itemType = item.GetType();

                // Try GetName
                var getNameMethod = itemType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(item, null) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        return TextUtilities.StripRichTextTags(name);
                    }
                }

                // Try to find card data inside
                var fields = itemType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("card") || fieldName.Contains("data"))
                    {
                        var value = field.GetValue(item);
                        if (value != null)
                        {
                            string info = CardTextReader.ExtractCardInfo(value);
                            if (!string.IsNullOrEmpty(info))
                                return info;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Extract info from a RelicUI component
        /// </summary>
        public static string ExtractRelicUIInfo(object relicUI)
        {
            if (relicUI == null) return null;

            try
            {
                var uiType = relicUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // Look for relicData field
                foreach (var field in fields)
                {
                    if (field.Name.ToLower().Contains("data") || field.Name.ToLower().Contains("relic"))
                    {
                        var data = field.GetValue(relicUI);
                        if (data != null)
                        {
                            string info = ExtractRewardDataInfo(data);
                            if (!string.IsNullOrEmpty(info))
                                return info;
                        }
                    }
                }

                // Try GetRelicData method
                var getDataMethod = uiType.GetMethod("GetRelicData", Type.EmptyTypes) ??
                                   uiType.GetMethod("GetData", Type.EmptyTypes);
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(relicUI, null);
                    if (data != null)
                    {
                        return ExtractRewardDataInfo(data);
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Find a component by type name in the hierarchy (up and down)
        /// </summary>
        public static Component FindComponentInHierarchy(GameObject go, string typeName)
        {
            // Check this object and children
            foreach (var comp in go.GetComponentsInChildren<Component>(true))
            {
                if (comp != null && comp.GetType().Name == typeName)
                    return comp;
            }

            // Check parents
            Transform current = go.transform.parent;
            while (current != null)
            {
                foreach (var comp in current.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == typeName)
                        return comp;
                }
                current = current.parent;
            }

            return null;
        }

        /// <summary>
        /// Extract info from MerchantGoodDetailsUI (card/relic/enhancer for sale).
        /// Structure: MerchantGoodDetailsUI → rewardUI (RewardDetailsUI)
        ///   → relicUI (RelicInfoUI with titleLabel + descriptionLabel)
        ///   → cardUI (CardUI)
        ///   → genericRewardUI (RewardIconUI)
        /// </summary>
        public static string ExtractMerchantGoodInfo(Component goodUI)
        {
            try
            {
                var uiType = goodUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // Look for rewardUI field - this contains the actual reward data
                var rewardUIField = fields.FirstOrDefault(f => f.Name == "rewardUI");
                object rewardUI = rewardUIField?.GetValue(goodUI);

                if (rewardUI != null)
                {
                    var rdType = rewardUI.GetType();
                    var rdFields = rdType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    // Detect the reward type so we know which child UI to read.
                    // RewardDetailsUI leaves all of relicUI / cardUI / genericRewardUI alive
                    // and toggles which GameObject is active per reward. The inactive ones
                    // retain stale label text (the game ships a "This should be the blessing
                    // description, but something went wrong." placeholder on RelicInfoUI's
                    // TMP asset), so we have to avoid reading them.
                    string rewardDataTypeName = null;
                    var rewardDataField = rdFields.FirstOrDefault(f =>
                        f.Name == "<RewardData>k__BackingField" || f.Name == "rewardData" ||
                        f.Name.IndexOf("RewardData", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (rewardDataField != null)
                    {
                        var rewardDataVal = rewardDataField.GetValue(rewardUI);
                        if (rewardDataVal != null)
                            rewardDataTypeName = rewardDataVal.GetType().Name;
                    }

                    var cardUIField = rdFields.FirstOrDefault(f => f.Name == "cardUI");
                    var relicUIField = rdFields.FirstOrDefault(f => f.Name == "relicUI");

                    bool cardUIActive = false;
                    if (cardUIField?.GetValue(rewardUI) is Component cardComp)
                        cardUIActive = cardComp.gameObject.activeInHierarchy;

                    bool isCardReward = cardUIActive ||
                        (rewardDataTypeName != null && rewardDataTypeName.Contains("CardRewardData"));

                    // For cards, delegate to CardUI extraction FIRST. Do this before relicUI
                    // so stale RelicInfoUI labels never win for card rewards.
                    if (isCardReward && cardUIField != null)
                    {
                        var cardUIObj = cardUIField.GetValue(rewardUI);
                        if (cardUIObj != null)
                        {
                            string cardInfo = CardTextReader.ExtractCardUIInfo(cardUIObj);
                            if (!string.IsNullOrEmpty(cardInfo))
                            {
                                string price = GetPriceFromBuyButton(goodUI.gameObject);
                                if (!string.IsNullOrEmpty(price))
                                    return $"{cardInfo}. {price}";
                                return cardInfo;
                            }
                        }
                    }

                    // For enhancers/relics/sins, read from RelicInfoUI labels.
                    if (!isCardReward && relicUIField != null)
                    {
                        var relicUIObj = relicUIField.GetValue(rewardUI);
                        if (relicUIObj is Component relicComp && relicComp.gameObject.activeInHierarchy)
                        {
                            string relicText = ReadRelicInfoUILabels(relicComp);
                            if (!string.IsNullOrEmpty(relicText))
                            {
                                string price = GetPriceFromBuyButton(goodUI.gameObject);
                                if (!string.IsNullOrEmpty(price))
                                    return $"{relicText}. {price}";
                                return relicText;
                            }
                        }
                    }

                    // Fallback to CardUI for any other case (e.g. data type not recognized).
                    if (!isCardReward && cardUIField != null)
                    {
                        var cardUIObj = cardUIField.GetValue(rewardUI);
                        if (cardUIObj != null)
                        {
                            string cardInfo = CardTextReader.ExtractCardUIInfo(cardUIObj);
                            if (!string.IsNullOrEmpty(cardInfo))
                            {
                                string price = GetPriceFromBuyButton(goodUI.gameObject);
                                if (!string.IsNullOrEmpty(price))
                                    return $"{cardInfo}. {price}";
                                return cardInfo;
                            }
                        }
                    }

                    // Fallback: try the full ExtractRewardUIInfo path
                    string rewardInfo = ExtractRewardUIInfo(rewardUI);
                    if (!string.IsNullOrEmpty(rewardInfo))
                    {
                        string price = GetPriceFromBuyButton(goodUI.gameObject);
                        if (!string.IsNullOrEmpty(price))
                            return $"{rewardInfo}. {price}";
                        return rewardInfo;
                    }
                }

                // Fallback: look for card data directly
                foreach (var field in fields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("card") || fieldName.Contains("good") || fieldName.Contains("data"))
                    {
                        var value = field.GetValue(goodUI);
                        if (value != null)
                        {
                            string cardInfo = CardTextReader.ExtractCardInfo(value);
                            if (!string.IsNullOrEmpty(cardInfo))
                            {
                                string price = GetPriceFromBuyButton(goodUI.gameObject);
                                if (!string.IsNullOrEmpty(price))
                                    return $"{cardInfo}. {price}";
                                return cardInfo;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting merchant good info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Read the titleLabel and descriptionLabel directly from a RelicInfoUI component.
        /// This works for enhancers, relics, and sins even when relicData is null.
        /// </summary>
        private static string ReadRelicInfoUILabels(Component relicInfoUI)
        {
            try
            {
                var riType = relicInfoUI.GetType();
                var riFields = riType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                string title = null;
                string description = null;

                var titleField = riFields.FirstOrDefault(f => f.Name == "titleLabel");
                if (titleField != null)
                {
                    var titleLabel = titleField.GetValue(relicInfoUI);
                    if (titleLabel != null)
                        title = UITextHelper.GetTextFromComponent(titleLabel);
                }

                var descField = riFields.FirstOrDefault(f => f.Name == "descriptionLabel");
                if (descField != null)
                {
                    var descLabel = descField.GetValue(relicInfoUI);
                    if (descLabel != null)
                        description = UITextHelper.GetTextFromComponent(descLabel);
                }

                // RelicInfoUI retains stale/default label text when the component is
                // inactive OR when it's attached to a RewardDetailsUI showing a different
                // reward type. The game ships a placeholder like "This should be the
                // blessing description, but something went wrong." on its default TMP
                // label asset — never useful to speak. If we see it in either slot,
                // treat the whole RelicInfoUI as having no content so the caller can
                // fall back to the CardUI/reward data path.
                bool IsBrokenText(string s) => !string.IsNullOrEmpty(s) &&
                    (s.IndexOf("This should be", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     s.IndexOf("something went wrong", StringComparison.OrdinalIgnoreCase) >= 0);

                bool titleBroken = IsBrokenText(title);
                bool descBroken = IsBrokenText(description);

                // Prefer relicData lookup whenever we suspect bad UI text.
                if (titleBroken || descBroken || string.IsNullOrEmpty(title))
                {
                    var relicDataProp = riType.GetProperty("relicData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var relicData = relicDataProp?.GetValue(relicInfoUI);
                    if (relicData != null)
                    {
                        var rdType = relicData.GetType();
                        if (titleBroken || string.IsNullOrEmpty(title))
                        {
                            var getName = rdType.GetMethod("GetName", Type.EmptyTypes);
                            var rdName = getName?.Invoke(relicData, null) as string;
                            title = string.IsNullOrEmpty(rdName) ? null : rdName;
                        }
                        if (descBroken || string.IsNullOrEmpty(description))
                        {
                            var getDesc = rdType.GetMethod("GetDescription", Type.EmptyTypes);
                            var rdDesc = getDesc?.Invoke(relicData, null) as string;
                            description = string.IsNullOrEmpty(rdDesc) || IsBrokenText(rdDesc) ? null : rdDesc;
                        }
                    }
                    else
                    {
                        // No backing relic data — the UI is just stale/empty. Bail so
                        // the caller's other branches (cardUI, generic reward) can run.
                        return null;
                    }
                }

                if (string.IsNullOrEmpty(title) || IsBrokenText(title)) return null;

                title = TextUtilities.StripRichTextTags(title);
                var sb = new StringBuilder();
                sb.Append(title);

                if (!string.IsNullOrEmpty(description))
                {
                    description = TextUtilities.CleanSpriteTagsForSpeech(description);
                    sb.Append($". {description}");

                    // Extract keyword definitions from the description
                    var keywords = new List<string>();
                    CardKeywordReader.ExtractKeywordsFromDescription(description, keywords);
                    if (keywords.Count > 0)
                        sb.Append($". Keywords: {string.Join(". ", keywords)}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"ReadRelicInfoUILabels error: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Extract info from a RewardUI component
        /// </summary>
        public static string ExtractRewardUIInfo(object rewardUI)
        {
            if (rewardUI == null) return null;

            try
            {
                var uiType = rewardUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var methods = uiType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                // Priority 1: Check rewardData backing field first - this has the actual data
                var rewardDataField = fields.FirstOrDefault(f =>
                    f.Name == "<rewardData>k__BackingField" || f.Name == "rewardData");
                if (rewardDataField != null)
                {
                    var rewardData = rewardDataField.GetValue(rewardUI);
                    if (rewardData != null)
                    {
                        string info = ExtractRewardDataInfo(rewardData);
                        if (!string.IsNullOrEmpty(info))
                            return info;
                    }
                }

                // Priority 2: Try GetRewardData method
                var getDataMethod = methods.FirstOrDefault(m =>
                    (m.Name == "GetRewardData" || m.Name == "GetData" || m.Name == "GetReward") &&
                    m.GetParameters().Length == 0);
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(rewardUI, null);
                    if (data != null)
                    {
                        string info = ExtractRewardDataInfo(data);
                        if (!string.IsNullOrEmpty(info))
                            return info;
                    }
                }

                // Priority 3: Check cardUI field for card rewards
                var cardUIField = fields.FirstOrDefault(f => f.Name == "cardUI");
                if (cardUIField != null)
                {
                    var cardUI = cardUIField.GetValue(rewardUI);
                    if (cardUI != null)
                    {
                        string cardInfo = CardTextReader.ExtractCardUIInfo(cardUI);
                        if (!string.IsNullOrEmpty(cardInfo))
                            return cardInfo;
                    }
                }

                // Priority 4: Check relicUI field (only if actively displayed —
                // the inactive RelicInfoUI retains the game's default placeholder text
                // "This should be the blessing description, but something went wrong.")
                var relicUIField = fields.FirstOrDefault(f => f.Name == "relicUI");
                if (relicUIField != null)
                {
                    var relicUI = relicUIField.GetValue(rewardUI);
                    if (relicUI is Component relicUIComp && relicUIComp.gameObject.activeInHierarchy)
                    {
                        string relicInfo = ExtractRelicUIInfo(relicUI);
                        if (!string.IsNullOrEmpty(relicInfo))
                            return relicInfo;
                    }
                }

                // Priority 5: Check genericRewardUI field
                var genericField = fields.FirstOrDefault(f => f.Name == "genericRewardUI");
                if (genericField != null)
                {
                    var genericUI = genericField.GetValue(rewardUI);
                    if (genericUI != null)
                    {
                        string genericInfo = ExtractGenericRewardUIInfo(genericUI);
                        if (!string.IsNullOrEmpty(genericInfo))
                            return genericInfo;
                    }
                }

                // Fallback: If rewardUI is a Component, check its GameObject for text
                if (rewardUI is Component comp)
                {
                    string textInfo = MapTextReader.GetFirstMeaningfulChildText(comp.gameObject);
                    if (!string.IsNullOrEmpty(textInfo))
                        return textInfo;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting reward UI info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract info from reward data (CardData, CardState, RelicData, etc.)
        /// Delegates to CardTextReader for card types to get full info (rarity, type, clan, etc.)
        /// </summary>
        public static string ExtractRewardDataInfo(object data)
        {
            if (data == null) return null;

            try
            {
                var dataType = data.GetType();
                string typeName = dataType.Name;

                // Special handling for EnhancerRewardData (upgrade stones like Surgestone)
                if (typeName == "EnhancerRewardData")
                {
                    string enhancerInfo = ExtractEnhancerInfo(data);
                    if (!string.IsNullOrEmpty(enhancerInfo))
                        return enhancerInfo;
                }

                // For CardState or CardData, use CardTextReader for full info
                if (typeName == "CardState" || typeName == "CardData")
                {
                    string cardInfo = CardTextReader.ExtractCardInfo(data);
                    if (!string.IsNullOrEmpty(cardInfo))
                        return cardInfo;
                }

                // For RelicData, extract name + description + keywords
                if (typeName.Contains("Relic"))
                {
                    return ExtractRelicDataInfo(data, dataType);
                }

                // Generic: try GetName + GetDescription
                var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(data, null) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        var parts = new List<string>();
                        parts.Add(TextUtilities.StripRichTextTags(name));

                        var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes);
                        if (getDescMethod != null)
                        {
                            var desc = getDescMethod.Invoke(data, null) as string;
                            if (!string.IsNullOrEmpty(desc))
                                parts.Add(TextUtilities.StripRichTextTags(desc));
                        }

                        return string.Join(". ", parts);
                    }
                }

                // Try fields as last resort
                var fields = dataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var nameField = fields.FirstOrDefault(f => f.Name.ToLower().Contains("name"));
                if (nameField != null)
                {
                    var name = nameField.GetValue(data) as string;
                    if (!string.IsNullOrEmpty(name))
                        return TextUtilities.StripRichTextTags(name);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting reward data: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract relic/artifact info with name, description, and keywords
        /// </summary>
        private static string ExtractRelicDataInfo(object relicData, Type dataType)
        {
            try
            {
                string name = null;
                string desc = null;

                var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                    name = getNameMethod.Invoke(relicData, null) as string;

                if (string.IsNullOrEmpty(name)) return null;

                // Try GetDescription first (returns empty if no translation context)
                try
                {
                    var method = dataType.GetMethod("GetDescription", Type.EmptyTypes);
                    if (method != null)
                        desc = method.Invoke(relicData, null) as string;
                }
                catch (Exception ex)
                {
                    MonsterTrainAccessibility.LogInfo($"ExtractRelicDataInfo GetDescription threw: {ex.Message}");
                }

                // Fallback: localize the descriptionKey directly and resolve effect placeholders
                if (string.IsNullOrEmpty(desc))
                {
                    var descKeyMethod = dataType.GetMethod("GetDescriptionKey", Type.EmptyTypes);
                    if (descKeyMethod != null)
                    {
                        var key = descKeyMethod.Invoke(relicData, null) as string;
                        if (!string.IsNullOrEmpty(key))
                        {
                            desc = LocalizationHelper.Localize(key);
                            if (!string.IsNullOrEmpty(desc))
                                desc = LocalizationHelper.ResolveEffectPlaceholders(desc, relicData, dataType);
                        }
                    }
                }

                var sb = new StringBuilder();
                sb.Append($"Artifact: {TextUtilities.StripRichTextTags(name)}");

                if (!string.IsNullOrEmpty(desc))
                {
                    string cleanDesc = TextUtilities.CleanSpriteTagsForSpeech(desc);
                    sb.Append($". {cleanDesc}");

                    var keywords = new List<string>();
                    CardKeywordReader.ExtractKeywordsFromDescription(desc, keywords);
                    if (keywords.Count > 0)
                    {
                        sb.Append($". Keywords: {string.Join(". ", keywords)}");
                    }
                }

                return sb.ToString();
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Extract info from MerchantServiceUI (upgrade/service)
        /// </summary>
        public static string ExtractMerchantServiceInfo(Component serviceUI)
        {
            try
            {
                var uiType = serviceUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var methods = uiType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                var go = serviceUI.gameObject;

                string serviceName = null;
                string serviceDesc = null;

                // Priority 0: Check if GameObject name contains service type
                string goName = go.name.ToLower();
                if (goName.Contains("reroll"))
                {
                    serviceName = "Reroll";
                }
                else if (goName.Contains("purge") || goName.Contains("remove"))
                {
                    serviceName = "Purge Card";
                }
                else if (goName.Contains("duplicate") || goName.Contains("copy"))
                {
                    serviceName = "Duplicate Card";
                }
                else if (goName.Contains("upgrade") || goName.Contains("enhance"))
                {
                    serviceName = "Upgrade Card";
                }
                else if (goName.Contains("heal") || goName.Contains("repair"))
                {
                    serviceName = "Heal";
                }
                else if (goName.Contains("unleash"))
                {
                    serviceName = "Unleash";
                }

                // Priority 1: Extract service index from GO name and get data from MerchantScreen
                if (string.IsNullOrEmpty(serviceName))
                {
                    // Parse service sign index from name like "Service sign 1", "Service sign 2"
                    int serviceIndex = -1;
                    var match = System.Text.RegularExpressions.Regex.Match(go.name, @"Service sign (\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        serviceIndex = int.Parse(match.Groups[1].Value) - 1; // Convert to 0-based
                    }

                    // Find MerchantScreen or MerchantScreenContent parent and get services list
                    var parentTransform = go.transform.parent;
                    while (parentTransform != null && string.IsNullOrEmpty(serviceName))
                    {
                        var parentGO = parentTransform.gameObject;
                        var parentComponents = parentGO.GetComponents<Component>();

                        foreach (var comp in parentComponents)
                        {
                            if (comp == null) continue;
                            var compType = comp.GetType();
                            var compName = compType.Name;

                            // Look for MerchantScreen or MerchantScreenContent
                            if (compName == "MerchantScreen" || compName == "MerchantScreenContent")
                            {
                                // Look for services list/array field
                                var compFields = compType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                                // First check sourceMerchantData which should contain the actual service definitions
                                var merchantDataField = compFields.FirstOrDefault(f => f.Name == "sourceMerchantData");
                                if (merchantDataField != null)
                                {
                                    var merchantData = merchantDataField.GetValue(comp);
                                    if (merchantData != null)
                                    {
                                        var mdType = merchantData.GetType();
                                        var mdFields = mdType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                                        // Look for services list in merchant data
                                        foreach (var mdField in mdFields)
                                        {
                                            string mdFieldName = mdField.Name.ToLower();
                                            if (mdFieldName.Contains("service"))
                                            {
                                                var servicesValue = mdField.GetValue(merchantData);
                                                if (servicesValue != null)
                                                {
                                                    if (servicesValue is System.Collections.IList servicesList && serviceIndex >= 0 && serviceIndex < servicesList.Count)
                                                    {
                                                        var svcData = servicesList[serviceIndex];
                                                        if (svcData != null)
                                                        {
                                                            var svcType = svcData.GetType();

                                                            var getNameMethod = svcType.GetMethod("GetName", Type.EmptyTypes);
                                                            if (getNameMethod != null)
                                                            {
                                                                serviceName = getNameMethod.Invoke(svcData, null) as string;
                                                            }

                                                            // Try GetDescription
                                                            var getDescMethod = svcType.GetMethod("GetDescription", Type.EmptyTypes) ??
                                                                               svcType.GetMethod("GetTooltipDescription", Type.EmptyTypes);
                                                            if (getDescMethod != null)
                                                            {
                                                                serviceDesc = getDescMethod.Invoke(svcData, null) as string;
                                                            }

                                                            if (!string.IsNullOrEmpty(serviceName)) break;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                if (!string.IsNullOrEmpty(serviceName)) break;

                                foreach (var field in compFields)
                                {
                                    string fieldName = field.Name.ToLower();
                                    var value = field.GetValue(comp);
                                    if (value == null) continue;

                                    // Look for services list
                                    if (fieldName.Contains("service"))
                                    {
                                        // If it's a list/array, try to get item by index
                                        if (value is System.Collections.IList list && serviceIndex >= 0 && serviceIndex < list.Count)
                                        {
                                            var serviceData = list[serviceIndex];
                                            if (serviceData != null)
                                            {
                                                var dataType = serviceData.GetType();

                                                var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                                                if (getNameMethod != null)
                                                {
                                                    serviceName = getNameMethod.Invoke(serviceData, null) as string;
                                                }

                                                var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes) ??
                                                                   dataType.GetMethod("GetTooltipDescription", Type.EmptyTypes);
                                                if (getDescMethod != null)
                                                {
                                                    serviceDesc = getDescMethod.Invoke(serviceData, null) as string;
                                                }

                                                if (!string.IsNullOrEmpty(serviceName)) break;
                                            }
                                        }

                                        // If it's a single service data, try GetName
                                        var valueType = value.GetType();
                                        var nameMethod = valueType.GetMethod("GetName", Type.EmptyTypes);
                                        if (nameMethod != null)
                                        {
                                            serviceName = nameMethod.Invoke(value, null) as string;
                                            if (!string.IsNullOrEmpty(serviceName))
                                            {
                                                break;
                                            }
                                        }
                                    }
                                }

                                if (!string.IsNullOrEmpty(serviceName)) break;
                            }
                        }

                        parentTransform = parentTransform.parent;
                    }
                }

                // Priority 2: Look for service data via properties on MerchantServiceUI
                if (string.IsNullOrEmpty(serviceName))
                {
                    // Check all properties on MerchantServiceUI
                    var props = uiType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    // Check GoodState property specifically - this likely contains the service data
                    var goodStateProp = props.FirstOrDefault(p => p.Name == "GoodState");
                    if (goodStateProp != null)
                    {
                        try
                        {
                            var goodState = goodStateProp.GetValue(serviceUI);
                            if (goodState != null)
                            {
                                var gsType = goodState.GetType();

                                var gsProps = gsType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                                // Check RewardData property - this should have the actual service info
                                var rewardDataProp = gsProps.FirstOrDefault(p => p.Name == "RewardData");
                                if (rewardDataProp != null)
                                {
                                    try
                                    {
                                        var rewardData = rewardDataProp.GetValue(goodState);
                                        if (rewardData != null)
                                        {
                                            var rdType = rewardData.GetType();

                                            // Map RewardData type name to friendly service name and description
                                            (serviceName, serviceDesc) = rdType.Name switch
                                            {
                                                "PurgeRewardData" => ("Purge Card", "Remove a card from your deck"),
                                                "RerollMerchantRewardData" => ("Re-roll", "Randomize and refresh the offered goods"),
                                                "DuplicateRewardData" => ("Duplicate Card", "Create a copy of a card in your deck"),
                                                "HealRewardData" => ("Heal", "Restore health to your Pyre"),
                                                "TrainRepairRewardData" => ("Train Repair", "Repair your train"),
                                                "UnleashRewardData" => ("Unleash", "Choose a Branded unit and unleash its power"),
                                                "UpgradeRewardData" => ("Upgrade", "Upgrade a card"),
                                                "EnhancerRewardData" => ("Upgrade Stone", null),
                                                _ => (null, null)
                                            };

                                            // If mapping didn't work, try GetName method
                                            if (string.IsNullOrEmpty(serviceName))
                                            {
                                                var getNameMethod = rdType.GetMethod("GetName", Type.EmptyTypes);
                                                if (getNameMethod != null)
                                                {
                                                    serviceName = getNameMethod.Invoke(rewardData, null) as string;
                                                }
                                            }

                                            // Only try to get description from game if we don't have one from mapping
                                            if (string.IsNullOrEmpty(serviceDesc))
                                            {
                                                // Try various methods for getting the description
                                                var rdMethods = rdType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                                                var descMethodNames = new[] { "GetDescription", "GetTooltipDescription", "GetRewardDescription", "GetLocalizedDescription" };

                                                foreach (var methodName in descMethodNames)
                                                {
                                                    var descMethod = rdMethods.FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == 0);
                                                    if (descMethod != null)
                                                    {
                                                        try
                                                        {
                                                            var desc = descMethod.Invoke(rewardData, null) as string;
                                                            if (!string.IsNullOrEmpty(desc) && !desc.Contains("__") && !desc.Contains("-v2"))
                                                            {
                                                                serviceDesc = desc;
                                                                break;
                                                            }
                                                        }
                                                        catch { }
                                                    }
                                                }
                                            }

                                            // Don't use description if it looks like a raw localization key
                                            if (!string.IsNullOrEmpty(serviceDesc) && (serviceDesc.Contains("__") || serviceDesc.Contains("-v2") || serviceDesc.StartsWith("$")))
                                            {
                                                serviceDesc = null;
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        MonsterTrainAccessibility.LogError($"Error reading RewardData: {ex.Message}");
                                    }
                                }

                                // Try GetName method on GoodState itself
                                if (string.IsNullOrEmpty(serviceName))
                                {
                                    var getNameMethod = gsType.GetMethod("GetName", Type.EmptyTypes);
                                    if (getNameMethod != null)
                                    {
                                        serviceName = getNameMethod.Invoke(goodState, null) as string;
                                    }
                                }

                                // Try GetDescription method
                                if (string.IsNullOrEmpty(serviceDesc))
                                {
                                    var getDescMethod = gsType.GetMethod("GetDescription", Type.EmptyTypes) ??
                                                       gsType.GetMethod("GetTooltipDescription", Type.EmptyTypes);
                                    if (getDescMethod != null)
                                    {
                                        serviceDesc = getDescMethod.Invoke(goodState, null) as string;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MonsterTrainAccessibility.LogError($"Error reading GoodState: {ex.Message}");
                        }
                    }

                    foreach (var prop in props)
                    {
                        string propName = prop.Name.ToLower();
                        if (propName.Contains("service") || propName.Contains("data"))
                        {
                            try
                            {
                                var value = prop.GetValue(serviceUI);
                                if (value != null)
                                {
                                    var valueType = value.GetType();
                                    var getNameMethod = valueType.GetMethod("GetName", Type.EmptyTypes);
                                    if (getNameMethod != null)
                                    {
                                        serviceName = getNameMethod.Invoke(value, null) as string;
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    // Check all methods for GetServiceData or similar
                    var allMethods = uiType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var getDataMethods = allMethods.Where(m =>
                        (m.Name.Contains("Service") || m.Name.Contains("Data")) &&
                        m.GetParameters().Length == 0 &&
                        m.ReturnType != typeof(void)).Take(5);

                    foreach (var method in getDataMethods)
                    {
                        try
                        {
                            var result = method.Invoke(serviceUI, null);
                            if (result != null)
                            {
                                var resultType = result.GetType();
                                var getNameMethod = resultType.GetMethod("GetName", Type.EmptyTypes);
                                if (getNameMethod != null)
                                {
                                    serviceName = getNameMethod.Invoke(result, null) as string;
                                    if (!string.IsNullOrEmpty(serviceName)) break;
                                }
                            }
                        }
                        catch { }
                    }
                }

                // Priority 2: Search all fields for data objects
                if (string.IsNullOrEmpty(serviceName))
                {
                    foreach (var field in fields)
                    {
                        string fieldName = field.Name.ToLower();
                        var value = field.GetValue(serviceUI);
                        if (value == null) continue;

                        // Check for service/data objects
                        if (fieldName.Contains("service") || fieldName.Contains("data"))
                        {
                            // Try to get name/description from data object
                            var dataType = value.GetType();

                            var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                            if (getNameMethod != null)
                            {
                                serviceName = getNameMethod.Invoke(value, null) as string;
                            }

                            var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes) ??
                                               dataType.GetMethod("GetTooltipDescription", Type.EmptyTypes);
                            if (getDescMethod != null)
                            {
                                serviceDesc = getDescMethod.Invoke(value, null) as string;
                            }

                            if (!string.IsNullOrEmpty(serviceName))
                                break;
                        }
                    }
                }

                // Priority 3: Try methods on the UI component itself
                if (string.IsNullOrEmpty(serviceName))
                {
                    var getNameMethod = methods.FirstOrDefault(m =>
                        m.Name == "GetServiceName" || m.Name == "GetName");
                    if (getNameMethod != null && getNameMethod.GetParameters().Length == 0)
                    {
                        serviceName = getNameMethod.Invoke(serviceUI, null) as string;
                    }
                }

                // Priority 4: Search child transforms directly for text
                if (string.IsNullOrEmpty(serviceName))
                {
                    var serviceGO = serviceUI.gameObject;

                    // Look for specific named children that might contain the title
                    var titleChildNames = new[] { "Title", "TitleLabel", "Name", "ServiceName", "TitleText", "Label", "Text" };
                    foreach (var childName in titleChildNames)
                    {
                        var titleChild = serviceGO.transform.Find(childName);
                        if (titleChild != null)
                        {
                            serviceName = GetTMPTextDirect(titleChild.gameObject);
                            if (!string.IsNullOrEmpty(serviceName))
                            {
                                break;
                            }
                        }
                    }

                    // If still not found, get all text from children
                    if (string.IsNullOrEmpty(serviceName))
                    {
                        var childTexts = UITextHelper.GetAllTextFromChildren(serviceGO);

                        if (childTexts.Count > 0)
                        {
                            serviceName = childTexts[0];

                            if (childTexts.Count > 1)
                            {
                                serviceDesc = childTexts[1];
                            }
                        }
                    }
                }

                // Priority 5: Try titleLabel and descriptionLabel fields as last resort
                if (string.IsNullOrEmpty(serviceName))
                {
                    var titleLabelField = fields.FirstOrDefault(f => f.Name == "titleLabel");
                    var descLabelField = fields.FirstOrDefault(f => f.Name == "descriptionLabel");

                    if (titleLabelField != null)
                    {
                        var titleLabel = titleLabelField.GetValue(serviceUI);
                        if (titleLabel != null)
                        {
                            serviceName = UITextHelper.GetTextFromComponent(titleLabel);
                        }
                    }

                    if (descLabelField != null)
                    {
                        var descLabel = descLabelField.GetValue(serviceUI);
                        if (descLabel != null)
                        {
                            serviceDesc = UITextHelper.GetTextFromComponent(descLabel);
                        }
                    }
                }

                // Priority 6: Check for text/label fields (as strings)
                if (string.IsNullOrEmpty(serviceName))
                {
                    foreach (var field in fields)
                    {
                        string fieldName = field.Name.ToLower();
                        var value = field.GetValue(serviceUI);

                        if (value is string str && !string.IsNullOrEmpty(str))
                        {
                            if (fieldName.Contains("name") || fieldName.Contains("title"))
                            {
                                serviceName = str;
                            }
                            else if (fieldName.Contains("desc"))
                            {
                                serviceDesc = str;
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(serviceName))
                {
                    serviceName = TextUtilities.StripRichTextTags(serviceName);
                    // MerchantServiceUI has no direct cost field — cost lives on the
                    // BuyButton child (field 'cost') or GoodState.Cost property.
                    string price = GetPriceFromBuyButton(serviceUI.gameObject);

                    List<string> parts = new List<string> { serviceName };

                    if (!string.IsNullOrEmpty(serviceDesc) && serviceDesc != serviceName)
                    {
                        parts.Add(TextUtilities.StripRichTextTags(serviceDesc));
                    }

                    if (!string.IsNullOrEmpty(price))
                    {
                        parts.Add(price);
                    }

                    return string.Join(". ", parts);
                }

                MonsterTrainAccessibility.LogWarning("Could not extract service name from MerchantServiceUI");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting merchant service info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get price from the BuyButton component or GoodState.Cost property.
        /// Game API: BuyButton has private fields 'cost' (gold) and 'pyreHealthCost'.
        /// MerchantGoodUIBase has GoodState property with Cost.
        /// </summary>
        public static string GetPriceFromBuyButton(GameObject go)
        {
            try
            {
                // Try BuyButton first (most reliable, shows actual displayed cost)
                Component buyButton = FindComponentInHierarchy(go, "BuyButton");
                if (buyButton != null)
                {
                    var btnType = buyButton.GetType();
                    var fields = btnType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    var costField = fields.FirstOrDefault(f => f.Name == "cost");
                    if (costField != null)
                    {
                        var costValue = costField.GetValue(buyButton);
                        if (costValue is int cost && cost > 0)
                        {
                            return $"{cost} gold";
                        }
                    }

                    // Check pyreHealthCost as alternate currency
                    var pyreCostField = fields.FirstOrDefault(f => f.Name == "pyreHealthCost");
                    if (pyreCostField != null)
                    {
                        var pyreCost = pyreCostField.GetValue(buyButton);
                        if (pyreCost is int pCost && pCost > 0)
                        {
                            return $"{pCost} pyre health";
                        }
                    }
                }

                // Fallback: try GoodState.Cost on MerchantGoodUIBase
                Component goodUI = FindComponentInHierarchy(go, "MerchantGoodDetailsUI")
                                ?? FindComponentInHierarchy(go, "MerchantServiceUI");
                if (goodUI != null)
                {
                    var goodStateProp = goodUI.GetType().GetProperty("GoodState", BindingFlags.Public | BindingFlags.Instance);
                    if (goodStateProp != null)
                    {
                        var goodState = goodStateProp.GetValue(goodUI);
                        if (goodState != null)
                        {
                            var costProp = goodState.GetType().GetProperty("Cost", BindingFlags.Public | BindingFlags.Instance);
                            if (costProp != null)
                            {
                                var cost = costProp.GetValue(goodState);
                                if (cost is int c && c > 0)
                                {
                                    return $"{c} gold";
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Extract info from a generic reward UI (RewardIconUI)
        /// </summary>
        public static string ExtractGenericRewardUIInfo(object genericUI)
        {
            if (genericUI == null) return null;

            try
            {
                var uiType = genericUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // Look for data field
                foreach (var field in fields)
                {
                    if (field.Name.ToLower().Contains("data") || field.Name.ToLower().Contains("reward"))
                    {
                        var data = field.GetValue(genericUI);
                        if (data != null && !data.GetType().Name.Contains("Transform"))
                        {
                            string info = ExtractRewardDataInfo(data);
                            if (!string.IsNullOrEmpty(info))
                                return info;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Extract info from EnhancerRewardData (upgrade stones like Surgestone, Emberstone, etc.)
        /// </summary>
        public static string ExtractEnhancerInfo(object enhancerRewardData)
        {
            if (enhancerRewardData == null) return null;

            try
            {
                var rewardType = enhancerRewardData.GetType();
                var fields = rewardType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // Look for the enhancerData field (the actual EnhancerData object)
                object enhancerData = null;
                var enhancerDataField = fields.FirstOrDefault(f =>
                    f.Name.ToLower().Contains("enhancerdata") ||
                    f.Name == "enhancer" ||
                    f.Name == "_enhancerData");

                if (enhancerDataField != null)
                {
                    enhancerData = enhancerDataField.GetValue(enhancerRewardData);
                }

                // Try GetEnhancerData method
                if (enhancerData == null)
                {
                    var getEnhancerMethod = rewardType.GetMethod("GetEnhancerData", Type.EmptyTypes)
                                         ?? rewardType.GetMethod("GetEnhancer", Type.EmptyTypes);
                    if (getEnhancerMethod != null)
                    {
                        enhancerData = getEnhancerMethod.Invoke(enhancerRewardData, null);
                    }
                }

                // Check backing field
                if (enhancerData == null)
                {
                    var backingField = fields.FirstOrDefault(f => f.Name == "<enhancerData>k__BackingField");
                    if (backingField != null)
                    {
                        enhancerData = backingField.GetValue(enhancerRewardData);
                    }
                }

                if (enhancerData != null)
                {
                    return ExtractEnhancerDataInfo(enhancerData);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting enhancer info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract info from EnhancerData (the actual upgrade stone data)
        /// </summary>
        public static string ExtractEnhancerDataInfo(object enhancerData)
        {
            if (enhancerData == null) return null;

            try
            {
                var dataType = enhancerData.GetType();
                var fields = dataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var methods = dataType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                string name = null;
                string description = null;

                // Try GetName method
                var getNameMethod = methods.FirstOrDefault(m => m.Name == "GetName" && m.GetParameters().Length == 0);
                if (getNameMethod != null)
                {
                    name = getNameMethod.Invoke(enhancerData, null) as string;
                }

                // Try GetDescription method
                var getDescMethod = methods.FirstOrDefault(m => m.Name == "GetDescription" && m.GetParameters().Length == 0);
                if (getDescMethod != null)
                {
                    description = getDescMethod.Invoke(enhancerData, null) as string;
                }

                // Try GetID and localize
                if (string.IsNullOrEmpty(name))
                {
                    var getIdMethod = methods.FirstOrDefault(m => m.Name == "GetID" && m.GetParameters().Length == 0);
                    if (getIdMethod != null)
                    {
                        string id = getIdMethod.Invoke(enhancerData, null) as string;
                        if (!string.IsNullOrEmpty(id))
                        {
                            // Try standard localization keys
                            name = LocalizationHelper.Localize($"{id}_EnhancerData_NameKey")
                                ?? LocalizationHelper.Localize($"EnhancerData_{id}_Name");
                            if (string.IsNullOrEmpty(description))
                            {
                                description = LocalizationHelper.Localize($"{id}_EnhancerData_DescriptionKey")
                                           ?? LocalizationHelper.Localize($"EnhancerData_{id}_Description");
                            }
                        }
                    }
                }

                // Try to get upgrade info from the CardUpgradeData
                if (string.IsNullOrEmpty(description))
                {
                    // EnhancerData stores upgrade in effects[0].GetParamCardUpgradeData()
                    var getEffectsMethod = methods.FirstOrDefault(m => m.Name == "GetEffects" && m.GetParameters().Length == 0);
                    if (getEffectsMethod != null)
                    {
                        var effects = getEffectsMethod.Invoke(enhancerData, null) as System.Collections.IList;
                        if (effects != null && effects.Count > 0)
                        {
                            var effect = effects[0];
                            var effectType = effect.GetType();
                            var getUpgradeMethod = effectType.GetMethod("GetParamCardUpgradeData", Type.EmptyTypes);
                            if (getUpgradeMethod != null)
                            {
                                var upgradeData = getUpgradeMethod.Invoke(effect, null);
                                if (upgradeData != null)
                                {
                                    description = CardTextReader.ExtractCardUpgradeDescription(upgradeData);
                                }
                            }
                        }
                    }
                }

                // Build result
                if (!string.IsNullOrEmpty(name))
                {
                    var parts = new List<string>();
                    parts.Add(TextUtilities.StripRichTextTags(name));
                    parts.Add("Upgrade");

                    if (!string.IsNullOrEmpty(description))
                    {
                        parts.Add(TextUtilities.StripRichTextTags(description));
                    }

                    // Add helper instruction
                    parts.Add("After selecting a card, press Enter to apply the upgrade");

                    return string.Join(". ", parts);
                }

                // Fallback: try name field
                var nameField = fields.FirstOrDefault(f => f.Name == "name");
                if (nameField != null)
                {
                    name = nameField.GetValue(enhancerData) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        return TextUtilities.StripRichTextTags(name) + " (Upgrade)";
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting enhancer data: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract the name from a RewardData object
        /// </summary>
        public static string GetRewardName(object rewardData)
        {
            if (rewardData == null) return null;

            try
            {
                var rewardType = rewardData.GetType();

                // RewardData.RewardTitle is the canonical property — it localizes the
                // reward title key and falls back to GetFallbackRewardTitle() on miss.
                var rewardTitleProp = rewardType.GetProperty("RewardTitle", BindingFlags.Public | BindingFlags.Instance);
                if (rewardTitleProp != null)
                {
                    var title = rewardTitleProp.GetValue(rewardData) as string;
                    if (!string.IsNullOrEmpty(title))
                        return TextUtilities.CleanSpriteTagsForSpeech(title);
                }

                // Try GetTitle method first (if it exists)
                var getTitleMethod = rewardType.GetMethod("GetTitle");
                if (getTitleMethod != null)
                {
                    var title = getTitleMethod.Invoke(rewardData, null) as string;
                    // Only use if it looks like a real name (not a key)
                    if (!string.IsNullOrEmpty(title) && !title.Contains("_") && !title.Contains("-"))
                        return title;
                }

                // Try to get the title key and localize it
                var titleKeyField = rewardType.GetField("_rewardTitleKey", BindingFlags.NonPublic | BindingFlags.Instance);
                if (titleKeyField != null)
                {
                    var titleKey = titleKeyField.GetValue(rewardData) as string;
                    if (!string.IsNullOrEmpty(titleKey))
                    {
                        // Try to localize the key
                        string localized = LocalizationHelper.Localize(titleKey);
                        // Only use if localization succeeded (not same as key and looks like real text)
                        if (!string.IsNullOrEmpty(localized) && localized != titleKey && !localized.Contains("-"))
                            return localized;
                    }
                }

                // Fall back to type name - this is the most reliable approach
                return GetRewardTypeDisplayName(rewardType);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting reward name: {ex.Message}");
                return "Reward";
            }
        }

        /// <summary>
        /// Get a human-readable display name from the reward type
        /// </summary>
        public static string GetRewardTypeDisplayName(Type rewardType)
        {
            string typeName = rewardType.Name;
            if (typeName.EndsWith("RewardData"))
                typeName = typeName.Substring(0, typeName.Length - "RewardData".Length);

            // Convert type name to readable format
            switch (typeName)
            {
                case "RelicPool": return "Random Artifact";
                case "Relic": return "Artifact";
                case "CardPool": return "Random Card";
                case "Card": return "Card";
                case "Gold": return "Gold";
                case "Health": return "Pyre Health";
                case "Crystal": return "Crystal";
                case "EnhancerPool": return "Random Upgrade";
                case "Enhancer": return "Upgrade";
                case "Draft": return "Card Draft";
                case "RelicDraft": return "Artifact Choice";
                default: return typeName;
            }
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

        private static string GetTMPText(GameObject go)
        {
            try
            {
                var components = go.GetComponentsInChildren<Component>();
                foreach (var component in components)
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                    {
                        var textProperty = type.GetProperty("text");
                        if (textProperty != null)
                        {
                            string text = textProperty.GetValue(component) as string;
                            if (!string.IsNullOrEmpty(text))
                                return text;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string GetDirectText(GameObject go)
        {
            string text = GetTMPText(go);
            if (!string.IsNullOrEmpty(text))
                return text.Trim();
            var uiText = go.GetComponentInChildren<UnityEngine.UI.Text>();
            if (uiText != null && !string.IsNullOrEmpty(uiText.text))
                return uiText.text.Trim();
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
