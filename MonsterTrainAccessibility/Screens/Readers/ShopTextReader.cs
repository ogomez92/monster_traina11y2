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

                    MonsterTrainAccessibility.LogInfo($"BuyButton fields: {string.Join(", ", buyFields.Select(f => f.Name))}");

                    // Look for good/service reference
                    foreach (var field in buyFields)
                    {
                        var value = field.GetValue(buyButton);
                        if (value == null) continue;

                        string typeName = value.GetType().Name;
                        if (typeName.Contains("Good") || typeName.Contains("Service") ||
                            typeName.Contains("Card") || typeName.Contains("Relic"))
                        {
                            MonsterTrainAccessibility.LogInfo($"Found {field.Name}: {typeName}");
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
        /// Extract info from MerchantGoodDetailsUI (card/relic for sale)
        /// </summary>
        public static string ExtractMerchantGoodInfo(Component goodUI)
        {
            try
            {
                var uiType = goodUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                MonsterTrainAccessibility.LogInfo($"MerchantGoodDetailsUI fields: {string.Join(", ", fields.Select(f => f.Name).Take(15))}");

                // Look for rewardUI field - this contains the actual reward data
                var rewardUIField = fields.FirstOrDefault(f => f.Name == "rewardUI");
                if (rewardUIField != null)
                {
                    var rewardUI = rewardUIField.GetValue(goodUI);
                    if (rewardUI != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found rewardUI: {rewardUI.GetType().Name}");
                        string rewardInfo = ExtractRewardUIInfo(rewardUI);
                        if (!string.IsNullOrEmpty(rewardInfo))
                        {
                            // Try to get price from parent BuyButton
                            string price = GetPriceFromBuyButton(goodUI.gameObject);
                            if (!string.IsNullOrEmpty(price))
                            {
                                return $"{rewardInfo}. {price}";
                            }
                            return rewardInfo;
                        }
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
                                {
                                    return $"{cardInfo}. {price}";
                                }
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

                MonsterTrainAccessibility.LogInfo($"RewardUI type: {uiType.Name}");

                // Priority 1: Check rewardData backing field first - this has the actual data
                var rewardDataField = fields.FirstOrDefault(f =>
                    f.Name == "<rewardData>k__BackingField" || f.Name == "rewardData");
                if (rewardDataField != null)
                {
                    var rewardData = rewardDataField.GetValue(rewardUI);
                    if (rewardData != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found rewardData: {rewardData.GetType().Name}");
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
                        MonsterTrainAccessibility.LogInfo($"GetRewardData returned: {data.GetType().Name}");
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
                        MonsterTrainAccessibility.LogInfo($"Found cardUI: {cardUI.GetType().Name}");
                        string cardInfo = CardTextReader.ExtractCardUIInfo(cardUI);
                        if (!string.IsNullOrEmpty(cardInfo))
                            return cardInfo;
                    }
                }

                // Priority 4: Check relicUI field
                var relicUIField = fields.FirstOrDefault(f => f.Name == "relicUI");
                if (relicUIField != null)
                {
                    var relicUI = relicUIField.GetValue(rewardUI);
                    if (relicUI != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"Found relicUI: {relicUI.GetType().Name}");
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
                        MonsterTrainAccessibility.LogInfo($"Found genericRewardUI: {genericUI.GetType().Name}");
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
        /// Extract info from reward data (CardData, RelicData, etc.)
        /// </summary>
        public static string ExtractRewardDataInfo(object data)
        {
            if (data == null) return null;

            try
            {
                var dataType = data.GetType();
                string typeName = dataType.Name;

                MonsterTrainAccessibility.LogInfo($"Extracting reward data from: {typeName}");

                // Special handling for EnhancerRewardData (upgrade stones like Surgestone)
                if (typeName == "EnhancerRewardData")
                {
                    string enhancerInfo = ExtractEnhancerInfo(data);
                    if (!string.IsNullOrEmpty(enhancerInfo))
                        return enhancerInfo;
                }

                // Try GetName method
                var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(data, null) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        MonsterTrainAccessibility.LogInfo($"GetName returned: {name}");

                        // Try to get description too
                        string desc = null;
                        var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes);
                        if (getDescMethod != null)
                        {
                            desc = getDescMethod.Invoke(data, null) as string;
                        }

                        // Try to get cost for cards
                        int cost = -1;
                        var getCostMethod = dataType.GetMethod("GetCost", Type.EmptyTypes);
                        if (getCostMethod != null)
                        {
                            var costResult = getCostMethod.Invoke(data, null);
                            if (costResult is int c)
                                cost = c;
                        }

                        List<string> parts = new List<string>();
                        parts.Add(TextUtilities.StripRichTextTags(name));

                        if (cost >= 0)
                            parts.Add($"{cost} ember");

                        if (!string.IsNullOrEmpty(desc))
                            parts.Add(TextUtilities.StripRichTextTags(desc));

                        return string.Join(". ", parts);
                    }
                }

                // For CardState, get CardData first
                if (typeName == "CardState")
                {
                    return CardTextReader.ExtractCardInfo(data);
                }

                // Try fields
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
        /// Extract info from MerchantServiceUI (upgrade/service)
        /// </summary>
        public static string ExtractMerchantServiceInfo(Component serviceUI)
        {
            try
            {
                var uiType = serviceUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var methods = uiType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                // Log the GameObject hierarchy - the name often contains the service type
                var go = serviceUI.gameObject;
                string hierarchyPath = go.name;
                var parent = go.transform.parent;
                while (parent != null)
                {
                    hierarchyPath = parent.name + "/" + hierarchyPath;
                    parent = parent.parent;
                    if (hierarchyPath.Length > 200) break; // Safety limit
                }
                MonsterTrainAccessibility.LogInfo($"MerchantServiceUI hierarchy: {hierarchyPath}");
                MonsterTrainAccessibility.LogInfo($"MerchantServiceUI fields: {string.Join(", ", fields.Select(f => f.Name).Take(20))}");

                // Log all components on this GameObject
                var components = go.GetComponents<Component>();
                MonsterTrainAccessibility.LogInfo($"Components on GO: {string.Join(", ", components.Select(c => c?.GetType().Name ?? "null"))}");

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

                if (!string.IsNullOrEmpty(serviceName))
                {
                    MonsterTrainAccessibility.LogInfo($"Got service name from GO name: {serviceName}");
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
                        MonsterTrainAccessibility.LogInfo($"Service sign index: {serviceIndex}");
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
                                MonsterTrainAccessibility.LogInfo($"Found parent component: {compType.Name}");

                                // Look for services list/array field
                                var compFields = compType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                MonsterTrainAccessibility.LogInfo($"{compName} fields: {string.Join(", ", compFields.Select(f => f.Name).Take(20))}");

                                // First check sourceMerchantData which should contain the actual service definitions
                                var merchantDataField = compFields.FirstOrDefault(f => f.Name == "sourceMerchantData");
                                if (merchantDataField != null)
                                {
                                    var merchantData = merchantDataField.GetValue(comp);
                                    if (merchantData != null)
                                    {
                                        var mdType = merchantData.GetType();
                                        MonsterTrainAccessibility.LogInfo($"sourceMerchantData type: {mdType.Name}");

                                        // Log all fields on merchant data
                                        var mdFields = mdType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                        MonsterTrainAccessibility.LogInfo($"MerchantData fields: {string.Join(", ", mdFields.Select(f => f.Name).Take(20))}");

                                        // Look for services list in merchant data
                                        foreach (var mdField in mdFields)
                                        {
                                            string mdFieldName = mdField.Name.ToLower();
                                            if (mdFieldName.Contains("service"))
                                            {
                                                var servicesValue = mdField.GetValue(merchantData);
                                                if (servicesValue != null)
                                                {
                                                    MonsterTrainAccessibility.LogInfo($"Found {mdField.Name}: {servicesValue.GetType().Name}");

                                                    if (servicesValue is System.Collections.IList servicesList && serviceIndex >= 0 && serviceIndex < servicesList.Count)
                                                    {
                                                        var svcData = servicesList[serviceIndex];
                                                        if (svcData != null)
                                                        {
                                                            var svcType = svcData.GetType();
                                                            MonsterTrainAccessibility.LogInfo($"Service[{serviceIndex}] type: {svcType.Name}");

                                                            // Log service data fields
                                                            var svcFields = svcType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                                            MonsterTrainAccessibility.LogInfo($"Service fields: {string.Join(", ", svcFields.Select(f => f.Name).Take(15))}");

                                                            var getNameMethod = svcType.GetMethod("GetName", Type.EmptyTypes);
                                                            if (getNameMethod != null)
                                                            {
                                                                serviceName = getNameMethod.Invoke(svcData, null) as string;
                                                                MonsterTrainAccessibility.LogInfo($"Service name from GetName(): {serviceName}");
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
                                        MonsterTrainAccessibility.LogInfo($"Found field {field.Name}: {value.GetType().Name}");

                                        // If it's a list/array, try to get item by index
                                        if (value is System.Collections.IList list && serviceIndex >= 0 && serviceIndex < list.Count)
                                        {
                                            var serviceData = list[serviceIndex];
                                            if (serviceData != null)
                                            {
                                                var dataType = serviceData.GetType();
                                                MonsterTrainAccessibility.LogInfo($"Service data type: {dataType.Name}");

                                                var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                                                if (getNameMethod != null)
                                                {
                                                    serviceName = getNameMethod.Invoke(serviceData, null) as string;
                                                    MonsterTrainAccessibility.LogInfo($"Service name from list[{serviceIndex}]: {serviceName}");
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
                                                MonsterTrainAccessibility.LogInfo($"Service name from {field.Name}: {serviceName}");
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
                    MonsterTrainAccessibility.LogInfo($"MerchantServiceUI properties: {string.Join(", ", props.Select(p => p.Name).Take(15))}");

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
                                MonsterTrainAccessibility.LogInfo($"GoodState type: {gsType.Name}");

                                // Log GoodState fields
                                var gsFields = gsType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                MonsterTrainAccessibility.LogInfo($"GoodState fields: {string.Join(", ", gsFields.Select(f => f.Name).Take(15))}");

                                // Log GoodState properties
                                var gsProps = gsType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                MonsterTrainAccessibility.LogInfo($"GoodState properties: {string.Join(", ", gsProps.Select(p => p.Name).Take(15))}");

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
                                            MonsterTrainAccessibility.LogInfo($"RewardData type: {rdType.Name}");

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

                                            if (!string.IsNullOrEmpty(serviceName))
                                            {
                                                MonsterTrainAccessibility.LogInfo($"Service name from type mapping: {serviceName}");
                                            }

                                            // If mapping didn't work, try GetName method
                                            if (string.IsNullOrEmpty(serviceName))
                                            {
                                                var getNameMethod = rdType.GetMethod("GetName", Type.EmptyTypes);
                                                if (getNameMethod != null)
                                                {
                                                    serviceName = getNameMethod.Invoke(rewardData, null) as string;
                                                    MonsterTrainAccessibility.LogInfo($"Service name from RewardData.GetName(): {serviceName}");
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
                                                                MonsterTrainAccessibility.LogInfo($"Got description from {methodName}: {serviceDesc}");
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
                                        MonsterTrainAccessibility.LogInfo($"Service name from GoodState.GetName(): {serviceName}");
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
                                        MonsterTrainAccessibility.LogInfo($"Service desc from GoodState: {serviceDesc}");
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
                                    MonsterTrainAccessibility.LogInfo($"Property {prop.Name}: {value.GetType().Name}");

                                    var valueType = value.GetType();
                                    var getNameMethod = valueType.GetMethod("GetName", Type.EmptyTypes);
                                    if (getNameMethod != null)
                                    {
                                        serviceName = getNameMethod.Invoke(value, null) as string;
                                        MonsterTrainAccessibility.LogInfo($"Service name from property: {serviceName}");
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
                        MonsterTrainAccessibility.LogInfo($"Found method: {method.Name} returns {method.ReturnType.Name}");
                        try
                        {
                            var result = method.Invoke(serviceUI, null);
                            if (result != null)
                            {
                                MonsterTrainAccessibility.LogInfo($"Method {method.Name} returned: {result.GetType().Name}");

                                var resultType = result.GetType();
                                var getNameMethod = resultType.GetMethod("GetName", Type.EmptyTypes);
                                if (getNameMethod != null)
                                {
                                    serviceName = getNameMethod.Invoke(result, null) as string;
                                    MonsterTrainAccessibility.LogInfo($"Service name from method result: {serviceName}");
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
                            MonsterTrainAccessibility.LogInfo($"Checking field {field.Name}: {value.GetType().Name}");

                            // Try to get name/description from data object
                            var dataType = value.GetType();

                            var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                            if (getNameMethod != null)
                            {
                                serviceName = getNameMethod.Invoke(value, null) as string;
                                MonsterTrainAccessibility.LogInfo($"Service name from {field.Name}: {serviceName}");
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
                        MonsterTrainAccessibility.LogInfo($"Service name from method: {serviceName}");
                    }
                }

                // Priority 4: Search child transforms directly for text
                if (string.IsNullOrEmpty(serviceName))
                {
                    var serviceGO = serviceUI.gameObject;

                    // Log all immediate children
                    var childNames = new List<string>();
                    for (int i = 0; i < serviceGO.transform.childCount; i++)
                    {
                        var child = serviceGO.transform.GetChild(i);
                        childNames.Add(child.name);

                        // Try to get text from each immediate child
                        string childText = GetTMPTextDirect(child.gameObject);
                        if (!string.IsNullOrEmpty(childText))
                        {
                            MonsterTrainAccessibility.LogInfo($"Found text in child '{child.name}': {childText}");
                        }

                        // Also check grandchildren
                        for (int j = 0; j < child.childCount; j++)
                        {
                            var grandchild = child.GetChild(j);
                            string gcText = GetTMPTextDirect(grandchild.gameObject);
                            if (!string.IsNullOrEmpty(gcText))
                            {
                                MonsterTrainAccessibility.LogInfo($"Found text in grandchild '{child.name}/{grandchild.name}': {gcText}");
                            }
                        }
                    }
                    MonsterTrainAccessibility.LogInfo($"MerchantServiceUI children: {string.Join(", ", childNames)}");

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
                                MonsterTrainAccessibility.LogInfo($"Got service name from child '{childName}': {serviceName}");
                                break;
                            }
                        }
                    }

                    // If still not found, get all text from children
                    if (string.IsNullOrEmpty(serviceName))
                    {
                        var childTexts = UITextHelper.GetAllTextFromChildren(serviceGO);
                        MonsterTrainAccessibility.LogInfo($"Child texts found: {string.Join(", ", childTexts.Take(5))}");

                        if (childTexts.Count > 0)
                        {
                            serviceName = childTexts[0];
                            MonsterTrainAccessibility.LogInfo($"Got service name from children: {serviceName}");

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
                            MonsterTrainAccessibility.LogInfo($"Got title from titleLabel field: {serviceName}");
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
                                MonsterTrainAccessibility.LogInfo($"Got service name from string field {field.Name}: {serviceName}");
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
                    string price = GetShopItemPrice(serviceUI);

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
        /// Get price from the BuyButton component
        /// </summary>
        public static string GetPriceFromBuyButton(GameObject go)
        {
            try
            {
                // Find BuyButton in hierarchy
                Component buyButton = FindComponentInHierarchy(go, "BuyButton");
                if (buyButton != null)
                {
                    var btnType = buyButton.GetType();
                    var fields = btnType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    // Look for cost field
                    var costField = fields.FirstOrDefault(f => f.Name == "cost");
                    if (costField != null)
                    {
                        var costValue = costField.GetValue(buyButton);
                        if (costValue is int cost && cost > 0)
                        {
                            return $"{cost} gold";
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
                    MonsterTrainAccessibility.LogInfo($"Found EnhancerData: {enhancerData.GetType().Name}");
                    return ExtractEnhancerDataInfo(enhancerData);
                }

                // If no enhancerData found, log available fields for debugging
                MonsterTrainAccessibility.LogInfo($"EnhancerRewardData fields: {string.Join(", ", fields.Select(f => f.Name).Take(15))}");
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

                    MonsterTrainAccessibility.LogInfo($"Enhancer result: {string.Join(". ", parts)}");
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
