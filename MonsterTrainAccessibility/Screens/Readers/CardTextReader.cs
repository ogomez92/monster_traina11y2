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
        /// Extract card info from CardState or CardData
        /// </summary>
        public static string ExtractCardInfo(object cardObj)
        {
            if (cardObj == null) return null;

            try
            {
                var cardType = cardObj.GetType();

                // If this is CardState, get CardData first
                object cardData = cardObj;
                if (cardType.Name == "CardState")
                {
                    var getDataMethod = cardType.GetMethod("GetCardDataRead", Type.EmptyTypes);
                    if (getDataMethod != null)
                    {
                        cardData = getDataMethod.Invoke(cardObj, null);
                        if (cardData == null) return null;
                    }
                }

                var dataType = cardData.GetType();
                string name = null;
                string description = null;
                int cost = -1;

                // Get name
                var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    name = getNameMethod.Invoke(cardData, null) as string;
                }

                // Get description
                var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    description = getDescMethod.Invoke(cardData, null) as string;
                }

                // Get cost
                var getCostMethod = dataType.GetMethod("GetCost", Type.EmptyTypes);
                if (getCostMethod != null)
                {
                    var costResult = getCostMethod.Invoke(cardData, null);
                    if (costResult is int c)
                        cost = c;
                }

                if (!string.IsNullOrEmpty(name))
                {
                    List<string> parts = new List<string>();
                    parts.Add(TextUtilities.StripRichTextTags(name));

                    if (cost >= 0)
                    {
                        parts.Add($"{cost} ember");
                    }

                    if (!string.IsNullOrEmpty(description))
                    {
                        parts.Add(TextUtilities.StripRichTextTags(description));
                    }

                    // Add keyword definitions
                    string keywords = GetCardKeywordTooltips(cardObj, cardData, description);
                    if (!string.IsNullOrEmpty(keywords))
                    {
                        parts.Add($"Keywords: {keywords}");
                    }

                    return string.Join(". ", parts);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting card info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Format card details into a readable string (name, type, clan, cost, description)
        /// </summary>
        public static string FormatCardDetails(object cardState)
        {
            try
            {
                var sb = new StringBuilder();
                var type = cardState.GetType();

                MonsterTrainAccessibility.LogInfo($"FormatCardDetails called for type: {type.Name}");

                // Get card name
                string name = "Unknown Card";
                var getTitleMethod = type.GetMethod("GetTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    name = getTitleMethod.Invoke(cardState, null) as string ?? "Unknown Card";
                    MonsterTrainAccessibility.LogInfo($"Card name: {name}");
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

                // Get CardData to access linked class (clan) and better descriptions
                object cardData = null;
                string clanName = null;
                string description = null;

                var getCardDataMethod = type.GetMethod("GetCardDataRead", Type.EmptyTypes)
                                     ?? type.GetMethod("GetCardData", Type.EmptyTypes);
                MonsterTrainAccessibility.LogInfo($"GetCardData method: {(getCardDataMethod != null ? getCardDataMethod.Name : "NOT FOUND")}");

                // Log all methods that might be related to card data
                var cardDataMethods = type.GetMethods()
                    .Where(m => m.Name.Contains("CardData") || m.Name.Contains("Data"))
                    .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                    .Distinct()
                    .ToArray();
                MonsterTrainAccessibility.LogInfo($"CardState data methods: {string.Join(", ", cardDataMethods)}");

                if (getCardDataMethod != null)
                {
                    cardData = getCardDataMethod.Invoke(cardState, null);
                    MonsterTrainAccessibility.LogInfo($"CardData result: {(cardData != null ? cardData.GetType().Name : "null")}");
                }

                if (cardData != null)
                {
                    var cardDataType = cardData.GetType();

                    // Get linked class (clan) from CardData
                    var linkedClassField = cardDataType.GetField("linkedClass", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (linkedClassField != null)
                    {
                        var linkedClass = linkedClassField.GetValue(cardData);
                        if (linkedClass != null)
                        {
                            var classType = linkedClass.GetType();
                            // Try GetTitle() for localized name
                            var getClassTitleMethod = classType.GetMethod("GetTitle", Type.EmptyTypes);
                            if (getClassTitleMethod != null)
                            {
                                clanName = getClassTitleMethod.Invoke(linkedClass, null) as string;
                            }
                            // Fallback to GetName()
                            if (string.IsNullOrEmpty(clanName))
                            {
                                var getClassNameMethod = classType.GetMethod("GetName", Type.EmptyTypes);
                                if (getClassNameMethod != null)
                                {
                                    clanName = getClassNameMethod.Invoke(linkedClass, null) as string;
                                }
                            }
                        }
                    }

                    // Try GetDescription from CardData for effect text
                    var getDescMethod = cardDataType.GetMethod("GetDescription", Type.EmptyTypes);
                    if (getDescMethod != null)
                    {
                        description = getDescMethod.Invoke(cardData, null) as string;
                    }

                    // If no parameterless GetDescription, try with RelicManager parameter
                    if (string.IsNullOrEmpty(description))
                    {
                        var allDescMethods = cardDataType.GetMethods().Where(m => m.Name.Contains("Description")).ToArray();
                        foreach (var descMethod in allDescMethods)
                        {
                            var ps = descMethod.GetParameters();
                            // Log available description methods for debugging
                            MonsterTrainAccessibility.LogInfo($"CardData has description method: {descMethod.Name}({string.Join(", ", ps.Select(p => p.ParameterType.Name))})");
                        }
                    }
                }

                // Try GetCardText on CardState - this is the main method for card effect text
                if (string.IsNullOrEmpty(description))
                {
                    // Log all GetCardText methods for debugging
                    var cardTextMethods = type.GetMethods().Where(m => m.Name == "GetCardText").ToArray();
                    MonsterTrainAccessibility.LogInfo($"Found {cardTextMethods.Length} GetCardText methods");
                    foreach (var method in cardTextMethods)
                    {
                        var ps = method.GetParameters();
                        MonsterTrainAccessibility.LogInfo($"  GetCardText({string.Join(", ", ps.Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
                    }

                    // Try GetCardText with no parameters first
                    var getCardTextMethod = type.GetMethod("GetCardText", Type.EmptyTypes);
                    if (getCardTextMethod != null)
                    {
                        description = getCardTextMethod.Invoke(cardState, null) as string;
                        MonsterTrainAccessibility.LogInfo($"GetCardText() returned: '{description}'");
                    }

                    // If no parameterless version, try with parameters
                    if (string.IsNullOrEmpty(description))
                    {
                        foreach (var method in cardTextMethods)
                        {
                            var ps = method.GetParameters();
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
                                description = method.Invoke(cardState, args) as string;
                                MonsterTrainAccessibility.LogInfo($"GetCardText with {ps.Length} params returned: '{description}'");
                                if (!string.IsNullOrEmpty(description)) break;
                            }
                            catch (Exception ex)
                            {
                                MonsterTrainAccessibility.LogInfo($"GetCardText failed: {ex.Message}");
                            }
                        }
                    }
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

                // Log if we still have no description
                if (string.IsNullOrEmpty(description))
                {
                    var cardTextMethods = type.GetMethods().Where(m => m.Name == "GetCardText").ToArray();
                    MonsterTrainAccessibility.LogInfo($"GetCardText methods: {string.Join(", ", cardTextMethods.Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})"))}");
                }

                // Build announcement: Name (Type), Clan, Cost. Effect.
                sb.Append(name);
                if (!string.IsNullOrEmpty(cardType))
                {
                    sb.Append($" ({cardType})");
                }
                if (!string.IsNullOrEmpty(clanName))
                {
                    sb.Append($", {clanName}");
                }
                sb.Append($", {cost} ember");

                if (!string.IsNullOrEmpty(description))
                {
                    // Strip rich text tags for screen reader output
                    description = TextUtilities.StripRichTextTags(description);
                    sb.Append($". {description}");
                }

                // For unit cards, try to get attack and health stats
                if (cardType == "Unit" || cardType == "Monster")
                {
                    MonsterTrainAccessibility.LogInfo($"Unit card detected, looking for stats. cardData is {(cardData != null ? "not null" : "NULL")}");
                    int attack = -1;
                    int health = -1;

                    // Try to get stats from CardState
                    var getAttackMethod = type.GetMethod("GetAttackDamage", Type.EmptyTypes);
                    MonsterTrainAccessibility.LogInfo($"GetAttackDamage on CardState: {(getAttackMethod != null ? "found" : "not found")}");
                    if (getAttackMethod != null)
                    {
                        var attackResult = getAttackMethod.Invoke(cardState, null);
                        if (attackResult is int a) attack = a;
                        MonsterTrainAccessibility.LogInfo($"Attack from CardState: {attack}");
                    }

                    var getHPMethod = type.GetMethod("GetHP", Type.EmptyTypes)
                                   ?? type.GetMethod("GetHealth", Type.EmptyTypes)
                                   ?? type.GetMethod("GetMaxHP", Type.EmptyTypes);
                    MonsterTrainAccessibility.LogInfo($"GetHP/Health on CardState: {(getHPMethod != null ? getHPMethod.Name : "not found")}");
                    if (getHPMethod != null)
                    {
                        var hpResult = getHPMethod.Invoke(cardState, null);
                        if (hpResult is int h) health = h;
                        MonsterTrainAccessibility.LogInfo($"Health from CardState: {health}");
                    }

                    // If not found on CardState, try GetSpawnCharacterData directly on CardState
                    MonsterTrainAccessibility.LogInfo($"Stats after CardState check: attack={attack}, health={health}");
                    if (attack < 0 || health < 0)
                    {
                        // GetSpawnCharacterData is directly on CardState, not CardData
                        var getSpawnCharMethod = type.GetMethod("GetSpawnCharacterData", Type.EmptyTypes);
                        MonsterTrainAccessibility.LogInfo($"GetSpawnCharacterData on CardState: {(getSpawnCharMethod != null ? "found" : "not found")}");
                        if (getSpawnCharMethod != null)
                        {
                            var charData = getSpawnCharMethod.Invoke(cardState, null);
                            MonsterTrainAccessibility.LogInfo($"SpawnCharacterData result: {(charData != null ? charData.GetType().Name : "null")}");
                            if (charData != null)
                            {
                                var charDataType = charData.GetType();

                                // Log all methods on character data
                                var charMethods = charDataType.GetMethods()
                                    .Where(m => m.Name.Contains("Attack") || m.Name.Contains("Damage") || m.Name.Contains("HP") || m.Name.Contains("Health") || m.Name.Contains("Size"))
                                    .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                                    .Distinct()
                                    .ToArray();
                                MonsterTrainAccessibility.LogInfo($"CharacterData stat methods available: {string.Join(", ", charMethods)}");

                                if (attack < 0)
                                {
                                    var charAttackMethod = charDataType.GetMethod("GetAttackDamage", Type.EmptyTypes);
                                    if (charAttackMethod != null)
                                    {
                                        var attackResult = charAttackMethod.Invoke(charData, null);
                                        if (attackResult is int a) attack = a;
                                        MonsterTrainAccessibility.LogInfo($"Attack from CharacterData: {attack}");
                                    }
                                }

                                if (health < 0)
                                {
                                    var charHPMethod = charDataType.GetMethod("GetHealth", Type.EmptyTypes)
                                                   ?? charDataType.GetMethod("GetHP", Type.EmptyTypes)
                                                   ?? charDataType.GetMethod("GetMaxHP", Type.EmptyTypes);
                                    if (charHPMethod != null)
                                    {
                                        var hpResult = charHPMethod.Invoke(charData, null);
                                        if (hpResult is int h) health = h;
                                        MonsterTrainAccessibility.LogInfo($"Health from CharacterData: {health}");
                                    }
                                }

                                // Log what methods are available if still not found
                                if (attack < 0 || health < 0)
                                {
                                    var statMethods = charDataType.GetMethods()
                                        .Where(m => m.Name.Contains("Attack") || m.Name.Contains("Damage") || m.Name.Contains("HP") || m.Name.Contains("Health") || m.Name.Contains("Size"))
                                        .Select(m => m.Name)
                                        .Distinct()
                                        .ToArray();
                                    MonsterTrainAccessibility.LogInfo($"CharacterData stat methods: {string.Join(", ", statMethods)}");
                                }
                            }
                        }
                    }

                    // Append unit stats
                    if (attack >= 0 || health >= 0)
                    {
                        var stats = new List<string>();
                        if (attack >= 0) stats.Add($"{attack} attack");
                        if (health >= 0) stats.Add($"{health} health");
                        sb.Append($". {string.Join(", ", stats)}");
                    }
                }

                // Get keyword tooltips (Permafrost, Frozen, Regen, etc.)
                // Pass the description we already have to avoid re-fetching
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
        /// Extract keywords from card description text and look up their definitions
        /// </summary>
        public static void ExtractKeywordsFromDescription(string description, List<string> tooltips)
        {
            if (string.IsNullOrEmpty(description)) return;

            // First, try to extract keywords from bold tags and look them up dynamically
            ExtractBoldKeywordsWithGameLookup(description, tooltips);

            // Known keywords to look for as fallback (case-insensitive)
            var knownKeywords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Trigger abilities
                { "Slay", "Slay: Triggers after dealing a killing blow" },
                { "Revenge", "Revenge: Triggers when this unit takes damage" },
                { "Strike", "Strike: Triggers when this unit attacks" },
                { "Extinguish", "Extinguish: Triggers when this unit dies" },
                { "Summon", "Summon: Triggers when this unit is played" },
                { "Incant", "Incant: Triggers when you play a spell on this floor" },
                { "Resolve", "Resolve: Triggers after combat" },
                { "Rally", "Rally: Triggers when you play a non-Morsel unit on this floor" },
                { "Harvest", "Harvest: Triggers when any unit on this floor dies" },
                { "Gorge", "Gorge: Triggers when this unit eats a Morsel" },
                { "Inspire", "Inspire: Triggers when gaining Echo on this floor" },
                { "Rejuvenate", "Rejuvenate: Triggers when healed, even at full health" },
                { "Action", "Action: Triggers at start of this unit's turn" },
                { "Hatch", "Hatch: Unit dies and triggers hatching ability" },
                { "Hunger", "Hunger: Triggers when an Eaten unit is summoned" },
                { "Armored", "Armored: Triggers when Armor is added" },
                // Buffs
                { "Armor", "Armor: Blocks damage before health, each point blocks one damage" },
                { "Rage", "Rage: +2 Attack per stack, decreases every turn" },
                { "Regen", "Regen: Restores 1 health per stack at end of turn" },
                { "Damage Shield", "Damage Shield: Nullifies the next source of damage" },
                { "Lifesteal", "Lifesteal: Heals for damage dealt when attacking" },
                { "Spikes", "Spikes: Attackers take 1 damage per stack" },
                { "Stealth", "Stealth: Not targeted in combat, loses 1 stack per turn" },
                { "Spell Shield", "Spell Shield: Absorbs the next damage spell" },
                { "Spellshield", "Spellshield: Absorbs the next damage spell" },
                { "Soul", "Soul: Powers Devourer of Death's Extinguish ability" },
                // Debuffs
                { "Frostbite", "Frostbite: Takes 1 damage per stack at end of turn" },
                { "Sap", "Sap: -2 Attack per stack, decreases every turn" },
                { "Dazed", "Dazed: Cannot attack or use Action/Resolve abilities" },
                { "Rooted", "Rooted: Prevents the next floor movement" },
                { "Emberdrain", "Emberdrain: Lose Ember at turn start, decreases each turn" },
                { "Heartless", "Heartless: Cannot be healed" },
                { "Melee Weakness", "Melee Weakness: Takes extra damage from next melee attack" },
                { "Spell Weakness", "Spell Weakness: Takes extra damage from next spell" },
                { "Reap", "Reap: Takes 1 damage per stack of Echo after combat" },
                // Unit effects
                { "Quick", "Quick: Attacks before enemy units" },
                { "Multistrike", "Multistrike: Attacks an additional time each turn" },
                { "Sweep", "Sweep: Attacks all enemy units" },
                { "Trample", "Trample: Excess damage hits the next enemy" },
                { "Burnout", "Burnout: Dies when counter reaches 0" },
                { "Endless", "Endless: Returns card to top of draw pile when killed" },
                { "Fragile", "Fragile: Dies if it loses any health" },
                { "Immobile", "Immobile: Cannot move between floors" },
                { "Inert", "Inert: Cannot attack unless it has Fuel" },
                { "Fuel", "Fuel: Allows Inert units to attack, loses 1 per turn" },
                { "Phased", "Phased: Cannot attack or be damaged/targeted" },
                { "Relentless", "Relentless: Attacks until floor cleared, then ascends" },
                { "Haste", "Haste: Moves directly from first to third floor" },
                { "Cardless", "Cardless: Not from a card, won't go to Consume pile" },
                { "Buffet", "Buffet: Can be eaten multiple times" },
                { "Shell", "Shell: Consumes Echo to remove stacks, triggers Hatch when depleted" },
                { "Silence", "Silence: Disables triggered abilities" },
                { "Silenced", "Silenced: Triggered abilities are disabled" },
                { "Purify", "Purify: Removes all debuffs at end of turn" },
                { "Enchant", "Enchant: Other friendly units on floor gain a bonus" },
                { "Shard", "Shard: Powers Solgard the Martyr's abilities" },
                { "Eaten", "Eaten: Will be eaten by front unit after combat" },
                // Card effects
                { "Consume", "Consume: Can only be played once per battle" },
                { "Frozen", "Frozen: Not discarded at end of turn" },
                { "Permafrost", "Permafrost: Gains Frozen when drawn" },
                { "Purge", "Purge: Removed from deck for the rest of the run" },
                { "Intrinsic", "Intrinsic: Starts in your opening hand" },
                { "Holdover", "Holdover: Returns to hand at end of turn" },
                { "Etch", "Etch: Permanently upgrade this card when consumed" },
                { "Offering", "Offering: Played automatically if discarded" },
                { "Reserve", "Reserve: Triggers if card remains in hand at end of turn" },
                { "Pyrebound", "Pyrebound: Only playable in Pyre Room or floor below" },
                { "Piercing", "Piercing: Damage ignores Armor and shields" },
                { "Magic Power", "Magic Power: Boosts spell damage and healing" },
                { "Attuned", "Attuned: Multiplies Magic Power effects by 5" },
                { "Infused", "Infused: Floor gains 1 Echo when played" },
                { "Extract", "Extract: Removes charged echoes when played" },
                { "Spellchain", "Spellchain: Creates a copy with +1 cost and Purge" },
                { "X Cost", "X Cost: Spends all remaining Ember, effect scales with amount" },
                { "Unplayable", "Unplayable: This card cannot be played" },
                // Unit actions
                { "Ascend", "Ascend: Move up a floor to the back" },
                { "Descend", "Descend: Move down a floor to the back" },
                { "Reform", "Reform: Return a defeated friendly unit to hand" },
                { "Sacrifice", "Sacrifice: Kill a friendly unit to play this card" },
                { "Cultivate", "Cultivate: Increase stats of lowest health friendly unit" },
                // Enemy effects
                { "Recover", "Recover: Restores health to friendly units after combat" }
            };

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
