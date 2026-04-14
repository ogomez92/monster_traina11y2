using HarmonyLib;
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
    /// Extracts text from card UI elements, keywords, and tooltips.
    /// </summary>
    public static class CardTextReader
    {
        /// <summary>
        /// Get full card details when arrowing over a card in the hand (CardUI component)
        /// </summary>
        public static string GetCardUIText(GameObject go)
        {
            try
            {
                // Find CardUI component on this object or in hierarchy
                Component cardUIComponent = null;

                // Check this object and its children first
                foreach (var component in go.GetComponentsInChildren<Component>())
                {
                    if (component == null) continue;
                    if (component.GetType().Name == "CardUI" || component.GetType().Name == "UnitAbilityCardUI")
                    {
                        cardUIComponent = component;
                        break;
                    }
                }

                // If not found, check parents
                if (cardUIComponent == null)
                {
                    Transform current = go.transform;
                    while (current != null && cardUIComponent == null)
                    {
                        foreach (var component in current.GetComponents<Component>())
                        {
                            if (component == null) continue;
                            if (component.GetType().Name == "CardUI" || component.GetType().Name == "UnitAbilityCardUI")
                            {
                                cardUIComponent = component;
                                break;
                            }
                        }
                        current = current.parent;
                    }
                }

                if (cardUIComponent == null)
                    return null;

                // Get CardState from CardUI
                var cardUIType = cardUIComponent.GetType();
                object cardState = null;

                // Try common field/property names for the card state reference
                var cardStateField = cardUIType.GetField("cardState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (cardStateField != null)
                {
                    cardState = cardStateField.GetValue(cardUIComponent);
                }

                // Try _cardState
                if (cardState == null)
                {
                    cardStateField = cardUIType.GetField("_cardState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (cardStateField != null)
                    {
                        cardState = cardStateField.GetValue(cardUIComponent);
                    }
                }

                // Try CardState property
                if (cardState == null)
                {
                    var cardStateProp = cardUIType.GetProperty("CardState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (cardStateProp != null)
                    {
                        cardState = cardStateProp.GetValue(cardUIComponent);
                    }
                }

                // Try GetCard or GetCardState method
                if (cardState == null)
                {
                    var getCardMethod = cardUIType.GetMethod("GetCard", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (getCardMethod != null && getCardMethod.GetParameters().Length == 0)
                    {
                        cardState = getCardMethod.Invoke(cardUIComponent, null);
                    }
                }

                if (cardState == null)
                {
                    var getCardStateMethod = cardUIType.GetMethod("GetCardState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (getCardStateMethod != null && getCardStateMethod.GetParameters().Length == 0)
                    {
                        cardState = getCardStateMethod.Invoke(cardUIComponent, null);
                    }
                }

                if (cardState == null)
                {
                    MonsterTrainAccessibility.LogInfo("CardUI found but couldn't get CardState");
                    return null;
                }

                // Format the card details
                return FormatCardDetails(cardState);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting card UI text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Extract info from a CardUI component
        /// </summary>
        public static string ExtractCardUIInfo(object cardUI)
        {
            if (cardUI == null) return null;

            try
            {
                var uiType = cardUI.GetType();
                var fields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // Look for cardState field
                var cardStateField = fields.FirstOrDefault(f =>
                    f.Name.ToLower().Contains("cardstate") || f.Name.ToLower().Contains("card"));
                if (cardStateField != null)
                {
                    var cardState = cardStateField.GetValue(cardUI);
                    if (cardState != null)
                    {
                        return ExtractCardInfo(cardState);
                    }
                }

                // Try GetCardState method
                var getCardMethod = uiType.GetMethod("GetCardState", Type.EmptyTypes);
                if (getCardMethod != null)
                {
                    var cardState = getCardMethod.Invoke(cardUI, null);
                    if (cardState != null)
                    {
                        return ExtractCardInfo(cardState);
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Extract card info from CardState or CardData.
        /// Used by shop items and other contexts where we have raw data objects.
        /// </summary>
        public static string ExtractCardInfo(object cardObj)
        {
            if (cardObj == null) return null;

            try
            {
                var objType = cardObj.GetType();

                // If this is CardState, delegate to FormatCardDetails for full info
                if (objType.Name == "CardState")
                {
                    return FormatCardDetails(cardObj);
                }

                // Otherwise this is CardData - extract what we can
                var dataType = objType;
                string name = null;
                string description = null;
                int cost = -1;

                // Get name
                var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    name = getNameMethod.Invoke(cardObj, null) as string;
                }

                // Get rarity
                string rarity = GetRarityString(cardObj, dataType);

                // Get card type
                string cardType = null;
                var getCardTypeMethod = dataType.GetMethod("GetCardType");
                if (getCardTypeMethod != null)
                {
                    var cardTypeObj = getCardTypeMethod.Invoke(cardObj, null);
                    if (cardTypeObj != null)
                    {
                        cardType = cardTypeObj.ToString();
                        if (cardType == "Monster") cardType = "Unit";
                    }
                }

                // Get clan
                string clanName = GetClanFromCardData(cardObj, dataType);

                // Get description
                var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    description = getDescMethod.Invoke(cardObj, null) as string;
                }

                // Get cost
                var getCostMethod = dataType.GetMethod("GetCost", Type.EmptyTypes);
                if (getCostMethod != null)
                {
                    var costResult = getCostMethod.Invoke(cardObj, null);
                    if (costResult is int c)
                        cost = c;
                }

                if (!string.IsNullOrEmpty(name))
                {
                    var sb = new StringBuilder();
                    sb.Append(TextUtilities.StripRichTextTags(name));

                    // Build type info: "Rare Hellhorned Unit"
                    var typeInfoParts = new List<string>();
                    if (!string.IsNullOrEmpty(rarity)) typeInfoParts.Add(rarity);
                    if (!string.IsNullOrEmpty(clanName)) typeInfoParts.Add(clanName);
                    if (!string.IsNullOrEmpty(cardType)) typeInfoParts.Add(cardType);
                    if (typeInfoParts.Count > 0)
                    {
                        sb.Append($", {string.Join(" ", typeInfoParts)}");
                    }

                    if (cost >= 0)
                    {
                        sb.Append($", {cost} ember");
                    }

                    if (!string.IsNullOrEmpty(description))
                    {
                        sb.Append($". {TextUtilities.StripRichTextTags(description)}");
                    }

                    // Add keyword definitions
                    string keywords = CardKeywordReader.GetCardKeywordTooltips(cardObj, cardObj, description);
                    if (!string.IsNullOrEmpty(keywords))
                    {
                        sb.Append($". Keywords: {keywords}");
                    }

                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting card info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Format card details into a readable string.
        /// Format: "Name, Rarity ClanName CardType, Cost ember. Description. Stats. Keywords."
        /// Example: "Hornbreaker Prince, Rare Hellhorned Unit, 2 ember. Deal 30 damage. 30 attack, 5 health."
        /// </summary>
        public static string FormatCardDetails(object cardState)
        {
            try
            {
                var sb = new StringBuilder();
                var type = cardState.GetType();

                // Get card name
                string name = "Unknown Card";
                var getTitleMethod = type.GetMethod("GetTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    name = getTitleMethod.Invoke(cardState, null) as string ?? "Unknown Card";
                }

                // Get card type
                string cardType = null;
                var getCardTypeMethod = type.GetMethod("GetCardType");
                if (getCardTypeMethod != null)
                {
                    var cardTypeObj = getCardTypeMethod.Invoke(cardState, null);
                    if (cardTypeObj != null)
                    {
                        cardType = cardTypeObj.ToString();
                        if (cardType == "Monster") cardType = "Unit";
                    }
                }

                // Get ember cost
                int cost = 0;
                var getCostMethod = type.GetMethod("GetCostWithoutAnyModifications", Type.EmptyTypes)
                                  ?? type.GetMethod("GetCost", Type.EmptyTypes);
                if (getCostMethod != null)
                {
                    var costResult = getCostMethod.Invoke(cardState, null);
                    if (costResult is int c) cost = c;
                }

                // Get rarity directly from CardState (has GetRarity())
                string rarity = GetRarityString(cardState, type);

                // Get subtype and size directly from CardState for unit cards
                string unitSubtype = null;
                int cardSize = -1;
                if (cardType == "Unit" || cardType == "Monster")
                {
                    unitSubtype = GetUnitSubtype(cardState, type);
                    cardSize = GetCardSize(cardState, type);
                }

                // Get CardData for clan and descriptions
                object cardData = null;
                string clanName = null;
                string description = null;

                var getCardDataMethod = type.GetMethod("GetCardDataRead", Type.EmptyTypes)
                                     ?? type.GetMethod("GetCardData", Type.EmptyTypes);

                if (getCardDataMethod != null)
                {
                    cardData = getCardDataMethod.Invoke(cardState, null);
                }

                if (cardData != null)
                {
                    var cardDataType = cardData.GetType();

                    // Get linked class (clan) from CardData
                    clanName = GetClanFromCardData(cardData, cardDataType);

                    // Try GetDescription from CardData for effect text
                    var getDescMethod = cardDataType.GetMethod("GetDescription", Type.EmptyTypes);
                    if (getDescMethod != null)
                    {
                        description = getDescMethod.Invoke(cardData, null) as string;
                    }
                }

                // Try GetCardText on CardState - this is the main method for card effect text
                if (string.IsNullOrEmpty(description))
                {
                    description = GetCardTextFromState(cardState, type);
                }

                // Fallback: try GetAssetDescription
                if (string.IsNullOrEmpty(description))
                {
                    var getAssetDescMethod = type.GetMethod("GetAssetDescription", Type.EmptyTypes);
                    if (getAssetDescMethod != null)
                    {
                        description = getAssetDescMethod.Invoke(cardState, null) as string;
                    }
                }

                // Build announcement: Name, Rarity Clan Type, Cost ember. Description.
                sb.Append(name);

                // Build the type info: "Rare Hellhorned Unit" or "Common Spell"
                var typeInfoParts = new List<string>();
                if (!string.IsNullOrEmpty(rarity)) typeInfoParts.Add(rarity);
                if (!string.IsNullOrEmpty(clanName)) typeInfoParts.Add(clanName);
                if (!string.IsNullOrEmpty(cardType)) typeInfoParts.Add(cardType);
                if (typeInfoParts.Count > 0)
                {
                    sb.Append($", {string.Join(" ", typeInfoParts)}");
                }

                // Unit subtype (e.g. "Imp", "Demon")
                if (!string.IsNullOrEmpty(unitSubtype))
                {
                    sb.Append($", {unitSubtype}");
                }

                // Card size for units
                if (cardSize > 1)
                {
                    sb.Append($", size {cardSize}");
                }

                sb.Append($", {cost} ember");

                if (!string.IsNullOrEmpty(description))
                {
                    description = TextUtilities.CleanSpriteTagsForSpeech(description);
                    description = TextUtilities.FixSingularGrammar(description);
                    description = TextUtilities.TrimTrailingPunctuation(description);
                    if (!string.IsNullOrEmpty(description))
                        sb.Append($". {description}");
                }

                // For unit cards, try to get attack and health stats
                if (cardType == "Unit" || cardType == "Monster")
                {
                    string stats = GetUnitStats(cardState, type);
                    if (!string.IsNullOrEmpty(stats))
                    {
                        sb.Append($". {stats}");
                    }
                }

                // Get keyword tooltips (Permafrost, Frozen, Regen, etc.)
                string keywordTooltips = CardKeywordReader.GetCardKeywordTooltips(cardState, cardData, description);
                if (!string.IsNullOrEmpty(keywordTooltips))
                {
                    sb.Append($". Keywords: {keywordTooltips}");
                }

                var result = sb.ToString();
                MonsterTrainAccessibility.LogInfo($"FormatCardDetails result: {result}");
                return result;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error formatting card details: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get rarity string from a CardState or CardData object.
        /// Game API: CardState.GetRarity() / CardData.GetRarity() -> CollectableRarity enum
        /// Attempts localization via game's localization system.
        /// </summary>
        public static string GetRarityString(object obj, Type objType)
        {
            try
            {
                string rarityStr = null;

                // Try GetRarity() method (exists on both CardState and CardData)
                var getRarityMethod = objType.GetMethod("GetRarity", Type.EmptyTypes)
                                   ?? objType.GetMethod("GetRarityType", Type.EmptyTypes);
                if (getRarityMethod != null)
                {
                    var rarityObj = getRarityMethod.Invoke(obj, null);
                    if (rarityObj != null)
                        rarityStr = rarityObj.ToString();
                }

                // Filter out non-meaningful values (Unset=-1, Starter=4, None)
                if (string.IsNullOrEmpty(rarityStr) || rarityStr == "Unset" || rarityStr == "Starter" || rarityStr == "None")
                    return null;

                // Try to localize the rarity name
                string[] locKeys = { $"Rarity_{rarityStr}", $"CollectionRarity_{rarityStr}", $"CardRarity_{rarityStr}" };
                foreach (var key in locKeys)
                {
                    string localized = LocalizationHelper.TryLocalize(key);
                    if (!string.IsNullOrEmpty(localized) && localized != key)
                        return localized;
                }

                // Return the enum name as-is (English) if localization fails
                return rarityStr;
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get clan name from CardData.
        /// Game API: CardData.GetLinkedClass() -> ClassData, ClassData.GetTitle() for localized name
        /// </summary>
        public static string GetClanFromCardData(object cardData, Type cardDataType)
        {
            try
            {
                // Try GetLinkedClass() method first (game API)
                var getLinkedClassMethod = cardDataType.GetMethod("GetLinkedClass", Type.EmptyTypes);
                if (getLinkedClassMethod != null)
                {
                    var linkedClass = getLinkedClassMethod.Invoke(cardData, null);
                    if (linkedClass != null)
                    {
                        return GetLocalizedName(linkedClass);
                    }
                }

                // Fallback: try linkedClass field
                var linkedClassField = cardDataType.GetField("linkedClass", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (linkedClassField != null)
                {
                    var linkedClass = linkedClassField.GetValue(cardData);
                    if (linkedClass != null)
                    {
                        return GetLocalizedName(linkedClass);
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get localized name from a game object via GetTitle() or GetName()
        /// </summary>
        private static string GetLocalizedName(object obj)
        {
            if (obj == null) return null;
            try
            {
                var objType = obj.GetType();
                var getTitleMethod = objType.GetMethod("GetTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    var name = getTitleMethod.Invoke(obj, null) as string;
                    if (!string.IsNullOrEmpty(name)) return name;
                }

                var getNameMethod = objType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    return getNameMethod.Invoke(obj, null) as string;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get unit subtype name (e.g. "Imp", "Demon", "Morsel") from spawn character data.
        /// Game API: CharacterData.GetLocalizedSubtype() returns first localized subtype.
        /// Also tries CharacterData.GetSubtypes() -> List of SubtypeData -> SubtypeData.LocalizedName
        /// </summary>
        public static string GetUnitSubtype(object cardState, Type cardStateType)
        {
            try
            {
                object charData = GetSpawnCharacterData(cardState, cardStateType);
                if (charData == null) return null;

                var charDataType = charData.GetType();

                // Try GetLocalizedSubtype() first - game API returns first localized subtype
                var getLocalizedSubtypeMethod = charDataType.GetMethod("GetLocalizedSubtype", Type.EmptyTypes);
                if (getLocalizedSubtypeMethod != null)
                {
                    var subtype = getLocalizedSubtypeMethod.Invoke(charData, null) as string;
                    if (!string.IsNullOrEmpty(subtype))
                        return subtype;
                }

                // Try GetSubtypes() -> List<SubtypeData> -> SubtypeData.LocalizedName property
                var getSubtypesMethod = charDataType.GetMethod("GetSubtypes", Type.EmptyTypes);
                if (getSubtypesMethod != null)
                {
                    var subtypes = getSubtypesMethod.Invoke(charData, null) as System.Collections.IList;
                    if (subtypes != null && subtypes.Count > 0)
                    {
                        var subtypeNames = new List<string>();
                        foreach (var subtypeData in subtypes)
                        {
                            if (subtypeData == null) continue;
                            var sdType = subtypeData.GetType();

                            // Check IsNone first
                            var isNoneProp = sdType.GetProperty("IsNone");
                            if (isNoneProp != null && (bool)isNoneProp.GetValue(subtypeData))
                                continue;

                            // Get LocalizedName property (SubtypeData._subtype.Localize())
                            var localizedNameProp = sdType.GetProperty("LocalizedName");
                            if (localizedNameProp != null)
                            {
                                var name = localizedNameProp.GetValue(subtypeData) as string;
                                if (!string.IsNullOrEmpty(name))
                                    subtypeNames.Add(name);
                            }
                        }
                        if (subtypeNames.Count > 0)
                            return string.Join(", ", subtypeNames);
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get card/unit size.
        /// Game API: CardState.GetSize() returns clamped 1-6 from spawned character.
        /// </summary>
        public static int GetCardSize(object cardState, Type cardStateType)
        {
            try
            {
                var getSizeMethod = cardStateType.GetMethod("GetSize", Type.EmptyTypes);
                if (getSizeMethod != null)
                {
                    var result = getSizeMethod.Invoke(cardState, null);
                    if (result is int size) return size;
                }

                // Fallback with bool param: GetSize(bool ignoreTempUpgrade)
                var getSizeMethodBool = cardStateType.GetMethod("GetSize", new[] { typeof(bool) });
                if (getSizeMethodBool != null)
                {
                    var result = getSizeMethodBool.Invoke(cardState, new object[] { false });
                    if (result is int size) return size;
                }
            }
            catch { }
            return -1;
        }

        /// <summary>
        /// Get SpawnCharacterData from a CardState.
        /// Game API: CardState.GetSpawnCharacterData() -> CharacterData?
        /// </summary>
        private static object GetSpawnCharacterData(object cardState, Type cardStateType)
        {
            try
            {
                var getSpawnCharMethod = cardStateType.GetMethod("GetSpawnCharacterData", Type.EmptyTypes);
                if (getSpawnCharMethod != null)
                {
                    return getSpawnCharMethod.Invoke(cardState, null);
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get card effect text from CardState using GetCardText methods
        /// </summary>
        private static string GetCardTextFromState(object cardState, Type type)
        {
            try
            {
                // Prefer the parameterized GetCardText(stats, save, includeTraits=true) variant —
                // the game's CardData.GetCardText fills in dynamic effect text only when
                // includeCurrentTraitEffectText is true. The parameterless overload just
                // returns the raw stored template, which can be missing effect content.
                var cardTextMethods = type.GetMethods().Where(m => m.Name == "GetCardText").ToArray();
                foreach (var method in cardTextMethods)
                {
                    var ps = method.GetParameters();
                    if (ps.Length == 0) continue;
                    try
                    {
                        var args = new object[ps.Length];
                        for (int i = 0; i < ps.Length; i++)
                        {
                            if (ps[i].ParameterType == typeof(bool))
                                args[i] = true;
                            else if (ps[i].ParameterType.IsValueType)
                                args[i] = Activator.CreateInstance(ps[i].ParameterType);
                            else
                                args[i] = null;
                        }
                        var desc = method.Invoke(cardState, args) as string;
                        if (!string.IsNullOrEmpty(desc)) return desc;
                    }
                    catch { }
                }

                // Last resort: parameterless GetCardText (raw stored template).
                var getCardTextMethod = type.GetMethod("GetCardText", Type.EmptyTypes);
                if (getCardTextMethod != null)
                {
                    var desc = getCardTextMethod.Invoke(cardState, null) as string;
                    if (!string.IsNullOrEmpty(desc)) return desc;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get unit attack and health stats as a formatted string.
        /// Game API: CardState.GetTotalAttackDamage() -> int, CardState.GetHealth() -> float
        /// Fallback: CharacterData.GetAttackDamage() -> int, CharacterData.GetHealth() -> int
        /// </summary>
        public static string GetUnitStats(object cardState, Type type)
        {
            try
            {
                int attack = -1;
                int health = -1;

                // Try CardState.GetTotalAttackDamage() (game API)
                var getAttackMethod = type.GetMethod("GetTotalAttackDamage", Type.EmptyTypes)
                                   ?? type.GetMethod("GetAttackDamage", Type.EmptyTypes);
                if (getAttackMethod != null)
                {
                    var attackResult = getAttackMethod.Invoke(cardState, null);
                    if (attackResult is int a) attack = a;
                }

                // Try CardState.GetHealth() - returns float in game
                var getHPMethod = type.GetMethod("GetHealth", Type.EmptyTypes)
                               ?? type.GetMethod("GetHP", Type.EmptyTypes)
                               ?? type.GetMethod("GetMaxHP", Type.EmptyTypes);
                if (getHPMethod != null)
                {
                    var hpResult = getHPMethod.Invoke(cardState, null);
                    if (hpResult is float f) health = (int)f;
                    else if (hpResult is int h) health = h;
                }

                // Fallback: CharacterData.GetAttackDamage() and GetHealth()
                if (attack < 0 || health < 0)
                {
                    var charData = GetSpawnCharacterData(cardState, type);
                    if (charData != null)
                    {
                        var charDataType = charData.GetType();

                        if (attack < 0)
                        {
                            var charAttackMethod = charDataType.GetMethod("GetAttackDamage", Type.EmptyTypes);
                            if (charAttackMethod != null)
                            {
                                var attackResult = charAttackMethod.Invoke(charData, null);
                                if (attackResult is int a) attack = a;
                            }
                        }

                        if (health < 0)
                        {
                            var charHPMethod = charDataType.GetMethod("GetHealth", Type.EmptyTypes);
                            if (charHPMethod != null)
                            {
                                var hpResult = charHPMethod.Invoke(charData, null);
                                if (hpResult is int h) health = h;
                                else if (hpResult is float f) health = (int)f;
                            }
                        }
                    }
                }

                if (attack >= 0 || health >= 0)
                {
                    var stats = new List<string>();
                    if (attack >= 0) stats.Add($"{attack} attack");
                    if (health >= 0) stats.Add($"{health} health");
                    return string.Join(", ", stats);
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Extract description from CardUpgradeData
        /// </summary>
        public static string ExtractCardUpgradeDescription(object upgradeData)
        {
            if (upgradeData == null) return null;

            try
            {
                var dataType = upgradeData.GetType();
                var parts = new List<string>();

                // Get upgrade title/name
                var getTitleMethod = dataType.GetMethod("GetUpgradeTitleForCardText", Type.EmptyTypes)
                                  ?? dataType.GetMethod("GetUpgradeTitle", Type.EmptyTypes)
                                  ?? dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    var title = getTitleMethod.Invoke(upgradeData, null) as string;
                    if (!string.IsNullOrEmpty(title))
                    {
                        parts.Add(TextUtilities.StripRichTextTags(title));
                    }
                }

                // Get upgrade description
                var getDescMethod = dataType.GetMethod("GetUpgradeDescription", Type.EmptyTypes)
                                 ?? dataType.GetMethod("GetDescription", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    var desc = getDescMethod.Invoke(upgradeData, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                    {
                        parts.Add(TextUtilities.StripRichTextTags(desc));
                    }
                }

                // If no description, try to extract stat bonuses
                if (parts.Count <= 1)
                {
                    var bonuses = ExtractUpgradeBonuses(upgradeData);
                    if (!string.IsNullOrEmpty(bonuses))
                    {
                        parts.Add(bonuses);
                    }
                }

                if (parts.Count > 0)
                {
                    return string.Join(". ", parts);
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Extract stat bonuses from CardUpgradeData
        /// </summary>
        public static string ExtractUpgradeBonuses(object upgradeData)
        {
            if (upgradeData == null) return null;

            try
            {
                var dataType = upgradeData.GetType();
                var bonuses = new List<string>();

                // Check common bonus methods/fields
                var getBonusDamageMethod = dataType.GetMethod("GetBonusDamage", Type.EmptyTypes);
                if (getBonusDamageMethod != null)
                {
                    var damage = getBonusDamageMethod.Invoke(upgradeData, null);
                    if (damage is int d && d != 0)
                    {
                        bonuses.Add($"{(d > 0 ? "+" : "")}{d} Magic Power");
                    }
                }

                var getBonusHPMethod = dataType.GetMethod("GetBonusHP", Type.EmptyTypes);
                if (getBonusHPMethod != null)
                {
                    var hp = getBonusHPMethod.Invoke(upgradeData, null);
                    if (hp is int h && h != 0)
                    {
                        bonuses.Add($"{(h > 0 ? "+" : "")}{h} Health");
                    }
                }

                var getCostReductionMethod = dataType.GetMethod("GetCostReduction", Type.EmptyTypes);
                if (getCostReductionMethod != null)
                {
                    var reduction = getCostReductionMethod.Invoke(upgradeData, null);
                    if (reduction is int r && r != 0)
                    {
                        bonuses.Add($"-{r} Ember cost");
                    }
                }

                // Check for added traits
                var getTraitsMethod = dataType.GetMethod("GetTraitDataUpgradeList", Type.EmptyTypes)
                                   ?? dataType.GetMethod("GetTraitDataUpgrades", Type.EmptyTypes);
                if (getTraitsMethod != null)
                {
                    var traits = getTraitsMethod.Invoke(upgradeData, null) as System.Collections.IList;
                    if (traits != null && traits.Count > 0)
                    {
                        foreach (var trait in traits)
                        {
                            var traitType = trait.GetType();
                            var getTraitNameMethod = traitType.GetMethod("GetName", Type.EmptyTypes)
                                                  ?? traitType.GetMethod("GetTraitStateName", Type.EmptyTypes);
                            if (getTraitNameMethod != null)
                            {
                                var traitName = getTraitNameMethod.Invoke(trait, null) as string;
                                if (!string.IsNullOrEmpty(traitName))
                                {
                                    // Format trait name
                                    traitName = traitName.Replace("CardTraitState", "").Replace("State", "");
                                    bonuses.Add($"Gain {traitName}");
                                }
                            }
                        }
                    }
                }

                if (bonuses.Count > 0)
                {
                    return string.Join(" and ", bonuses);
                }
            }
            catch { }

            return null;
        }

    }
}
