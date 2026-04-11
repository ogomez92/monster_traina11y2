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
        private static Dictionary<string, string> _keywordCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool _keywordCacheInitialized = false;

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
                    if (component.GetType().Name == "CardUI")
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
                            if (component.GetType().Name == "CardUI")
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
                    string keywords = GetCardKeywordTooltips(cardObj, cardObj, description);
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
                    description = TextUtilities.StripRichTextTags(description);
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
                string keywordTooltips = GetCardKeywordTooltips(cardState, cardData, description);
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
                // Try GetCardText with no parameters first
                var getCardTextMethod = type.GetMethod("GetCardText", Type.EmptyTypes);
                if (getCardTextMethod != null)
                {
                    var desc = getCardTextMethod.Invoke(cardState, null) as string;
                    if (!string.IsNullOrEmpty(desc)) return desc;
                }

                // If no parameterless version, try with parameters
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
        /// Get keyword tooltip definitions from a card (Permafrost, Frozen, Regen, etc.)
        /// Returns formatted string of keyword definitions
        /// </summary>
        public static string GetCardKeywordTooltips(object cardState, object cardData, string cardDescription = null)
        {
            try
            {
                var tooltips = new List<string>();

                // Method 1: Get trait states from CardState - these have localized tooltip methods
                if (cardState != null)
                {
                    var stateType = cardState.GetType();

                    // Get trait states - CardTraitState objects have GetCardTooltipTitle/Text methods
                    var getTraitStatesMethod = stateType.GetMethod("GetTraitStates", Type.EmptyTypes);
                    if (getTraitStatesMethod != null)
                    {
                        var traitStates = getTraitStatesMethod.Invoke(cardState, null) as System.Collections.IEnumerable;
                        if (traitStates != null)
                        {
                            foreach (var traitState in traitStates)
                            {
                                if (traitState == null) continue;
                                string traitTooltip = ExtractTraitStateTooltip(traitState);
                                if (!string.IsNullOrEmpty(traitTooltip) && !tooltips.Any(t => t.StartsWith(traitTooltip.Split(':')[0] + ":")))
                                {
                                    tooltips.Add(traitTooltip);
                                }
                            }
                        }
                    }

                    // Also try GetEffectTooltipData or similar methods
                    var getTooltipsMethod = stateType.GetMethods()
                        .FirstOrDefault(m => m.Name.Contains("Tooltip") && m.GetParameters().Length == 0);
                    if (getTooltipsMethod != null)
                    {
                        var tooltipResult = getTooltipsMethod.Invoke(cardState, null);
                        if (tooltipResult is System.Collections.IList tooltipList)
                        {
                            foreach (var tooltip in tooltipList)
                            {
                                string tooltipText = ExtractTooltipText(tooltip);
                                if (!string.IsNullOrEmpty(tooltipText) && !tooltips.Contains(tooltipText))
                                    tooltips.Add(tooltipText);
                            }
                        }
                    }
                }

                // Method 2: Get tooltips from CardData's effects
                if (cardData != null)
                {
                    var dataType = cardData.GetType();

                    // Get card effects - each effect can have additionalTooltips
                    var getEffectsMethod = dataType.GetMethod("GetEffects", Type.EmptyTypes);
                    if (getEffectsMethod != null)
                    {
                        var effects = getEffectsMethod.Invoke(cardData, null) as System.Collections.IList;
                        if (effects != null)
                        {
                            foreach (var effect in effects)
                            {
                                // Get additionalTooltips from each effect
                                ExtractTooltipsFromEffect(effect, tooltips);

                                // Also get status effect tooltips from paramStatusEffects
                                ExtractStatusEffectTooltips(effect, tooltips);
                            }
                        }
                    }

                    // Also check card traits for tooltips (fallback for CardTraitData)
                    var getTraitsMethod = dataType.GetMethod("GetTraits", Type.EmptyTypes);
                    if (getTraitsMethod != null)
                    {
                        var traits = getTraitsMethod.Invoke(cardData, null) as System.Collections.IList;
                        if (traits != null)
                        {
                            foreach (var trait in traits)
                            {
                                ExtractTraitTooltip(trait, tooltips);
                            }
                        }
                    }
                }

                // Method 3: Parse keywords from card description and look up definitions
                // Use the passed description if available, otherwise try to fetch it
                string desc = cardDescription;
                if (string.IsNullOrEmpty(desc))
                {
                    desc = GetCardDescriptionForKeywordParsing(cardState, cardData);
                }
                if (!string.IsNullOrEmpty(desc))
                {
                    ExtractKeywordsFromDescription(desc, tooltips);
                }

                if (tooltips.Count > 0)
                {
                    return string.Join(". ", tooltips.Distinct());
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting keyword tooltips: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Extract keywords from card description text and look up their definitions.
        /// Uses KeywordManager for definitions (loaded from game localization + fallbacks).
        /// </summary>
        public static void ExtractKeywordsFromDescription(string description, List<string> tooltips)
        {
            if (string.IsNullOrEmpty(description)) return;

            // First, try to extract keywords from bold tags and look them up dynamically
            ExtractBoldKeywordsWithGameLookup(description, tooltips);

            // Use KeywordManager as the single source of truth for keyword definitions
            var knownKeywords = Core.KeywordManager.GetKeywords();

            foreach (var keyword in knownKeywords)
            {
                // Check if keyword appears in description (as whole word)
                if (System.Text.RegularExpressions.Regex.IsMatch(description,
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(keyword.Key)}\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    if (!tooltips.Contains(keyword.Value))
                    {
                        tooltips.Add(keyword.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Extract tooltip from a CardTraitState object using its localized methods
        /// </summary>
        public static string ExtractTraitStateTooltip(object traitState)
        {
            if (traitState == null) return null;

            try
            {
                var traitType = traitState.GetType();

                // Try GetCardTooltipTitle() - returns localized title like "Avance"
                string title = null;
                var getTitleMethod = traitType.GetMethod("GetCardTooltipTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    title = getTitleMethod.Invoke(traitState, null) as string;
                }

                // If no title method, try GetCardText()
                if (string.IsNullOrEmpty(title))
                {
                    var getCardTextMethod = traitType.GetMethod("GetCardText", Type.EmptyTypes);
                    if (getCardTextMethod != null)
                    {
                        title = getCardTextMethod.Invoke(traitState, null) as string;
                    }
                }

                // Try GetCardTooltipText() - returns localized description like "Se coloca en primera posición"
                string body = null;
                var getBodyMethod = traitType.GetMethod("GetCardTooltipText", Type.EmptyTypes);
                if (getBodyMethod != null)
                {
                    body = getBodyMethod.Invoke(traitState, null) as string;
                }

                // If methods didn't work, try localization key fallback
                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(body))
                {
                    string traitTypeName = traitType.Name;

                    // Try to get localized text using the game's key pattern
                    if (string.IsNullOrEmpty(title))
                    {
                        string titleKey = $"{traitTypeName}_CardText";
                        title = LocalizationHelper.Localize(titleKey);
                        if (title == titleKey) title = null; // Localization failed
                    }

                    if (string.IsNullOrEmpty(body))
                    {
                        string bodyKey = $"{traitTypeName}_TooltipText";
                        body = LocalizationHelper.Localize(bodyKey);
                        if (body == bodyKey) body = null; // Localization failed
                    }
                }

                // Clean up text
                title = TextUtilities.StripRichTextTags(title);
                body = TextUtilities.StripRichTextTags(body);

                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(body))
                {
                    MonsterTrainAccessibility.LogInfo($"Found trait tooltip: {title}: {body}");
                    return $"{title}: {body}";
                }
                else if (!string.IsNullOrEmpty(title))
                {
                    return title;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting trait state tooltip: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract tooltip text from a tooltip data object
        /// </summary>
        public static string ExtractTooltipText(object tooltip)
        {
            if (tooltip == null) return null;

            try
            {
                var tooltipType = tooltip.GetType();
                string title = null;
                string body = null;

                // Try GetTitle/GetBody methods
                var getTitleMethod = tooltipType.GetMethod("GetTitle", Type.EmptyTypes);
                var getBodyMethod = tooltipType.GetMethod("GetBody", Type.EmptyTypes)
                                 ?? tooltipType.GetMethod("GetDescription", Type.EmptyTypes);

                if (getTitleMethod != null)
                    title = getTitleMethod.Invoke(tooltip, null) as string;
                if (getBodyMethod != null)
                    body = getBodyMethod.Invoke(tooltip, null) as string;

                // Try title/body fields
                if (string.IsNullOrEmpty(title))
                {
                    var titleField = tooltipType.GetField("title", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                  ?? tooltipType.GetField("_title", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (titleField != null)
                        title = titleField.GetValue(tooltip) as string;
                }

                if (string.IsNullOrEmpty(body))
                {
                    var bodyField = tooltipType.GetField("body", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                 ?? tooltipType.GetField("description", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (bodyField != null)
                        body = bodyField.GetValue(tooltip) as string;
                }

                // Localize if needed
                title = LocalizeIfNeeded(title);
                body = LocalizeIfNeeded(body);

                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(body))
                {
                    return $"{TextUtilities.StripRichTextTags(title)}: {TextUtilities.StripRichTextTags(body)}";
                }
                else if (!string.IsNullOrEmpty(title))
                {
                    return TextUtilities.StripRichTextTags(title);
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Extract tooltips from a card effect (CardEffectData)
        /// </summary>
        public static void ExtractTooltipsFromEffect(object effect, List<string> tooltips)
        {
            if (effect == null) return;

            try
            {
                var effectType = effect.GetType();

                // Get additionalTooltips field
                var tooltipsField = effectType.GetField("additionalTooltips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (tooltipsField != null)
                {
                    var additionalTooltips = tooltipsField.GetValue(effect) as System.Collections.IList;
                    if (additionalTooltips != null)
                    {
                        foreach (var tooltip in additionalTooltips)
                        {
                            string text = ExtractAdditionalTooltipData(tooltip);
                            if (!string.IsNullOrEmpty(text) && !tooltips.Contains(text))
                                tooltips.Add(text);
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Extract tooltip from AdditionalTooltipData
        /// </summary>
        public static string ExtractAdditionalTooltipData(object tooltipData)
        {
            if (tooltipData == null) return null;

            try
            {
                var type = tooltipData.GetType();

                // AdditionalTooltipData has titleKey, descriptionKey, or title/description
                string title = null;
                string description = null;

                // Try titleKey/descriptionKey first
                var titleKeyField = type.GetField("titleKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var descKeyField = type.GetField("descriptionKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (titleKeyField != null)
                {
                    string key = titleKeyField.GetValue(tooltipData) as string;
                    title = LocalizationHelper.Localize(key);
                }

                if (descKeyField != null)
                {
                    string key = descKeyField.GetValue(tooltipData) as string;
                    description = LocalizationHelper.Localize(key);
                }

                // Also try direct title/description
                if (string.IsNullOrEmpty(title))
                {
                    var titleField = type.GetField("title", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (titleField != null)
                        title = titleField.GetValue(tooltipData) as string;
                }

                if (string.IsNullOrEmpty(description))
                {
                    var descField = type.GetField("description", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (descField != null)
                        description = descField.GetValue(tooltipData) as string;
                }

                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(description))
                {
                    return $"{TextUtilities.StripRichTextTags(title)}: {TextUtilities.StripRichTextTags(description)}";
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Extract status effect tooltips from a card effect's paramStatusEffects
        /// </summary>
        public static void ExtractStatusEffectTooltips(object effect, List<string> tooltips)
        {
            if (effect == null) return;

            try
            {
                var effectType = effect.GetType();

                // Get paramStatusEffects field
                var statusField = effectType.GetField("paramStatusEffects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (statusField != null)
                {
                    var statusEffects = statusField.GetValue(effect) as System.Collections.IList;
                    if (statusEffects != null)
                    {
                        foreach (var statusEffect in statusEffects)
                        {
                            string tooltip = GetStatusEffectTooltip(statusEffect);
                            if (!string.IsNullOrEmpty(tooltip) && !tooltips.Contains(tooltip))
                                tooltips.Add(tooltip);
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Get tooltip for a status effect stack/application
        /// </summary>
        public static string GetStatusEffectTooltip(object statusEffectParam)
        {
            if (statusEffectParam == null) return null;

            try
            {
                var type = statusEffectParam.GetType();

                // Get the statusId
                string statusId = null;
                var statusIdField = type.GetField("statusId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (statusIdField != null)
                {
                    statusId = statusIdField.GetValue(statusEffectParam) as string;
                }

                if (!string.IsNullOrEmpty(statusId))
                {
                    // Look up the status effect data
                    return GetStatusEffectDefinition(statusId);
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Extract trait tooltips from CardTraitData
        /// </summary>
        public static void ExtractTraitTooltip(object trait, List<string> tooltips)
        {
            if (trait == null) return;

            try
            {
                var traitType = trait.GetType();

                // Get trait name
                var getNameMethod = traitType.GetMethod("GetTraitStateName", Type.EmptyTypes)
                                 ?? traitType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    string traitName = getNameMethod.Invoke(trait, null) as string;
                    if (!string.IsNullOrEmpty(traitName))
                    {
                        // Look up trait definition
                        string tooltip = GetTraitDefinition(traitName);
                        if (!string.IsNullOrEmpty(tooltip) && !tooltips.Contains(tooltip))
                            tooltips.Add(tooltip);
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Get tooltip definition for a card trait
        /// </summary>
        public static string GetTraitDefinition(string traitName)
        {
            // Common card traits with their definitions
            var traitDefinitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "permafrost", "Permafrost: Card remains in hand when drawn" },
                { "frozen", "Frozen: Cannot be played until unfrozen" },
                { "consume", "Consume: Removed from deck after playing" },
                { "holdover", "Holdover: Returns to hand at end of turn" },
                { "purge", "Purge: Removed from deck permanently" },
                { "exhaust", "Exhaust: Removed from deck for this battle" },
                { "intrinsic", "Intrinsic: Always drawn on first turn" },
                { "etch", "Etch: Permanently upgrade this card" }
            };

            if (traitDefinitions.TryGetValue(traitName, out string definition))
            {
                return definition;
            }

            // Try localization
            string key = $"CardTrait_{traitName}_Tooltip";
            string localized = LocalizationHelper.Localize(key);
            if (!string.IsNullOrEmpty(localized) && localized != key)
            {
                return $"{FormatTraitName(traitName)}: {TextUtilities.StripRichTextTags(localized)}";
            }

            return null;
        }

        /// <summary>
        /// Format a trait name for display
        /// </summary>
        public static string FormatTraitName(string traitName)
        {
            if (string.IsNullOrEmpty(traitName)) return traitName;

            // Remove "State" suffix and format
            traitName = traitName.Replace("State", "");
            return System.Text.RegularExpressions.Regex.Replace(traitName, "([a-z])([A-Z])", "$1 $2");
        }

        /// <summary>
        /// Get card description for keyword parsing
        /// </summary>
        public static string GetCardDescriptionForKeywordParsing(object cardState, object cardData)
        {
            string desc = null;

            try
            {
                if (cardState != null)
                {
                    var type = cardState.GetType();
                    var getCardTextMethod = type.GetMethod("GetCardText", Type.EmptyTypes);
                    if (getCardTextMethod != null)
                    {
                        desc = getCardTextMethod.Invoke(cardState, null) as string;
                    }
                }

                if (string.IsNullOrEmpty(desc) && cardData != null)
                {
                    var dataType = cardData.GetType();
                    var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes);
                    if (getDescMethod != null)
                    {
                        desc = getDescMethod.Invoke(cardData, null) as string;
                    }
                }
            }
            catch { }

            return desc;
        }

        /// <summary>
        /// Extract bold keywords from text and look up their descriptions from the game
        /// </summary>
        public static void ExtractBoldKeywordsWithGameLookup(string description, List<string> tooltips)
        {
            if (string.IsNullOrEmpty(description)) return;

            // Extract text inside <b> tags - these are typically keywords
            var boldMatches = System.Text.RegularExpressions.Regex.Matches(description, @"<b>([^<]+)</b>");
            foreach (System.Text.RegularExpressions.Match match in boldMatches)
            {
                string keyword = match.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(keyword)) continue;

                // Skip numbers and short strings
                if (keyword.Length <= 1 || int.TryParse(keyword, out _)) continue;

                // Try to get description from game
                string keywordDescription = GetKeywordDescriptionFromGame(keyword);
                if (!string.IsNullOrEmpty(keywordDescription))
                {
                    string tooltip = $"{keyword}: {keywordDescription}";
                    if (!tooltips.Any(t => t.StartsWith(keyword + ":", StringComparison.OrdinalIgnoreCase)))
                    {
                        tooltips.Add(tooltip);
                        MonsterTrainAccessibility.LogInfo($"Found keyword from game: {tooltip}");
                    }
                }
            }
        }

        /// <summary>
        /// Get a keyword's description from the game's data
        /// </summary>
        public static string GetKeywordDescriptionFromGame(string keywordName)
        {
            if (string.IsNullOrEmpty(keywordName)) return null;

            // Check cache first
            if (_keywordCache.TryGetValue(keywordName, out string cached))
            {
                return cached;
            }

            try
            {
                // Initialize cache if needed
                if (!_keywordCacheInitialized)
                {
                    InitializeKeywordCache();
                    _keywordCacheInitialized = true;

                    // Check cache again after init
                    if (_keywordCache.TryGetValue(keywordName, out cached))
                    {
                        return cached;
                    }
                }

                // Try standard localization key patterns for keywords/status effects
                string[] keyPatterns = new[]
                {
                    $"StatusEffect_{keywordName}_CardText",
                    $"StatusEffect_{keywordName}_Description",
                    $"Keyword_{keywordName}_Description",
                    $"Tooltip_{keywordName}_Body",
                    $"CardTrait_{keywordName}_CardText",
                    $"CardTrait_{keywordName}_Description",
                    $"Trigger_{keywordName}_Description",
                    $"{keywordName}_Description",
                    $"{keywordName}_Tooltip"
                };

                foreach (var pattern in keyPatterns)
                {
                    string localized = LocalizationHelper.Localize(pattern);
                    if (!string.IsNullOrEmpty(localized) && localized != pattern && !localized.Contains("KEY>"))
                    {
                        _keywordCache[keywordName] = localized;
                        return localized;
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error looking up keyword '{keywordName}': {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Initialize the keyword cache by loading all status effects from the game
        /// </summary>
        public static void InitializeKeywordCache()
        {
            try
            {
                // Try to find StatusEffectManager or similar
                var allManagersType = AccessTools.TypeByName("AllGameManagers");
                if (allManagersType == null) return;

                var instanceProp = allManagersType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp == null) return;

                var instance = instanceProp.GetValue(null);
                if (instance == null) return;

                // Try to get StatusEffectManager
                var statusManagerProp = allManagersType.GetProperty("StatusEffectManager");
                if (statusManagerProp != null)
                {
                    var statusManager = statusManagerProp.GetValue(instance);
                    if (statusManager != null)
                    {
                        // Try to get all status effects
                        var getDataMethod = statusManager.GetType().GetMethod("GetAllStatusEffectData") ??
                                           statusManager.GetType().GetMethod("GetStatusEffectDatas");
                        if (getDataMethod != null)
                        {
                            var allData = getDataMethod.Invoke(statusManager, null) as System.Collections.IEnumerable;
                            if (allData != null)
                            {
                                foreach (var data in allData)
                                {
                                    if (data == null) continue;
                                    var dataType = data.GetType();

                                    // Get name
                                    var getNameMethod = dataType.GetMethod("GetStatusId") ?? dataType.GetMethod("GetName");
                                    string name = getNameMethod?.Invoke(data, null) as string;
                                    if (string.IsNullOrEmpty(name)) continue;

                                    // Get description
                                    var getDescMethod = dataType.GetMethod("GetDescription") ?? dataType.GetMethod("GetCardText");
                                    string desc = getDescMethod?.Invoke(data, null) as string;
                                    if (!string.IsNullOrEmpty(desc) && !desc.Contains("KEY>"))
                                    {
                                        desc = TextUtilities.StripRichTextTags(desc);
                                        _keywordCache[name] = desc;
                                    }
                                }
                                MonsterTrainAccessibility.LogInfo($"Loaded {_keywordCache.Count} keywords from game");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error initializing keyword cache: {ex.Message}");
            }
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

        /// <summary>
        /// Get the definition of a status effect by its ID
        /// </summary>
        public static string GetStatusEffectDefinition(string statusId)
        {
            if (string.IsNullOrEmpty(statusId)) return null;

            try
            {
                // Try to get from StatusEffectManager
                var managerType = ReflectionHelper.GetTypeFromAssemblies("StatusEffectManager");
                if (managerType != null)
                {
                    var instanceProp = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProp != null)
                    {
                        var manager = instanceProp.GetValue(null);
                        if (manager != null)
                        {
                            var getAllMethod = managerType.GetMethod("GetAllStatusEffectsData", Type.EmptyTypes);
                            if (getAllMethod != null)
                            {
                                var allData = getAllMethod.Invoke(manager, null);
                                if (allData != null)
                                {
                                    var getDataMethod = allData.GetType().GetMethod("GetStatusEffectData", Type.EmptyTypes);
                                    if (getDataMethod != null)
                                    {
                                        var dataList = getDataMethod.Invoke(allData, null) as System.Collections.IList;
                                        if (dataList != null)
                                        {
                                            foreach (var data in dataList)
                                            {
                                                var dataType = data.GetType();
                                                var getIdMethod = dataType.GetMethod("GetStatusId", Type.EmptyTypes);
                                                if (getIdMethod != null)
                                                {
                                                    string id = getIdMethod.Invoke(data, null) as string;
                                                    if (id == statusId)
                                                    {
                                                        // Found it - get name and description
                                                        return GetStatusEffectNameAndDescription(data, dataType);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Fallback: try localization directly
                string locKey = GetStatusEffectLocKey(statusId);
                string name = LocalizationHelper.Localize($"{locKey}_CardText");
                string desc = LocalizationHelper.Localize($"{locKey}_CardTooltipText");

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(desc) && name != $"{locKey}_CardText")
                {
                    return $"{TextUtilities.StripRichTextTags(name)}: {TextUtilities.StripRichTextTags(desc)}";
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get name and description from StatusEffectData
        /// </summary>
        public static string GetStatusEffectNameAndDescription(object statusData, Type dataType)
        {
            try
            {
                string name = null;
                string description = null;

                // Try GetDisplayName or similar
                var getNameMethod = dataType.GetMethod("GetDisplayName", Type.EmptyTypes)
                                 ?? dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    name = getNameMethod.Invoke(statusData, null) as string;
                }

                // Try GetDescription
                var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes)
                                 ?? dataType.GetMethod("GetTooltipText", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    description = getDescMethod.Invoke(statusData, null) as string;
                }

                // Fallback to localization
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(description))
                {
                    var getIdMethod = dataType.GetMethod("GetStatusId", Type.EmptyTypes);
                    if (getIdMethod != null)
                    {
                        string statusId = getIdMethod.Invoke(statusData, null) as string;
                        string locKey = GetStatusEffectLocKey(statusId);

                        if (string.IsNullOrEmpty(name))
                            name = LocalizationHelper.Localize($"{locKey}_CardText");
                        if (string.IsNullOrEmpty(description))
                            description = LocalizationHelper.Localize($"{locKey}_CardTooltipText");
                    }
                }

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(description))
                {
                    return $"{TextUtilities.StripRichTextTags(name)}: {TextUtilities.StripRichTextTags(description)}";
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get localization key prefix for a status effect ID
        /// </summary>
        public static string GetStatusEffectLocKey(string statusId)
        {
            if (string.IsNullOrEmpty(statusId)) return null;

            // Standard format: StatusEffect_[StatusId] with first letter capitalized
            if (statusId.Length == 1)
                return "StatusEffect_" + char.ToUpper(statusId[0]);
            else if (statusId.Length > 1)
                return "StatusEffect_" + char.ToUpper(statusId[0]) + statusId.Substring(1);

            return null;
        }

        /// <summary>
        /// Localize a string if it looks like a localization key
        /// </summary>
        public static string LocalizeIfNeeded(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // If it looks like a localization key, try to localize
            if (text.Contains("_") && !text.Contains(" "))
            {
                string localized = LocalizationHelper.Localize(text);
                if (!string.IsNullOrEmpty(localized) && localized != text)
                    return localized;
            }

            return text;
        }

    }
}
