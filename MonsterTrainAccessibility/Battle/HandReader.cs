using MonsterTrainAccessibility.Core;
using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace MonsterTrainAccessibility.Battle
{
    /// <summary>
    /// Reads hand/card information from the game's CardManager via reflection.
    /// </summary>
    public class HandReader
    {
        private readonly BattleManagerCache _cache;

        public HandReader(BattleManagerCache cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// Announce all cards in hand
        /// </summary>
        public void AnnounceHand()
        {
            try
            {
                var hand = GetHandCards();
                if (hand == null || hand.Count == 0)
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak("Hand is empty", false);
                    return;
                }

                var sb = new StringBuilder();
                sb.Append($"Hand contains {hand.Count} cards. ");

                int currentEnergy = GetCurrentEnergy();
                var verbosity = MonsterTrainAccessibility.AccessibilitySettings.VerbosityLevel.Value;

                for (int i = 0; i < hand.Count; i++)
                {
                    var card = hand[i];
                    string name = GetCardTitle(card);
                    int cost = GetCardCost(card);
                    string cardType = GetCardType(card);
                    string clanName = GetCardClan(card);
                    string description = GetCardDescription(card);

                    string playable = (currentEnergy >= 0 && cost > currentEnergy) ? ", unplayable" : "";

                    // Build card announcement based on verbosity
                    if (verbosity == VerbosityLevel.Minimal)
                    {
                        sb.Append($"{i + 1}: {name}, {cost} ember{playable}. ");
                    }
                    else
                    {
                        // Normal and Verbose include type, clan, and description
                        string typeStr = !string.IsNullOrEmpty(cardType) ? $" ({cardType})" : "";
                        string clanStr = !string.IsNullOrEmpty(clanName) ? $", {clanName}" : "";
                        sb.Append($"{i + 1}: {name}{typeStr}{clanStr}, {cost} ember{playable}. ");

                        if (!string.IsNullOrEmpty(description))
                        {
                            sb.Append($"{description} ");
                        }

                        // At Verbose level, include keyword tooltips
                        if (verbosity == VerbosityLevel.Verbose)
                        {
                            string keywords = GetCardKeywords(card, description);
                            if (!string.IsNullOrEmpty(keywords))
                            {
                                sb.Append($"Keywords: {keywords} ");
                            }
                        }
                    }
                }

                MonsterTrainAccessibility.ScreenReader?.Speak(sb.ToString(), false);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error announcing hand: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Could not read hand", false);
            }
        }

        /// <summary>
        /// Get the list of card objects in hand
        /// </summary>
        public List<object> GetHandCards()
        {
            if (_cache.CardManager == null || _cache.GetHandMethod == null)
            {
                _cache.FindManagers();
                if (_cache.CardManager == null) return null;
            }

            try
            {
                var result = _cache.GetHandMethod.Invoke(_cache.CardManager, new object[] { false });
                if (result is System.Collections.IList list)
                {
                    var cards = new List<object>();
                    foreach (var card in list)
                    {
                        cards.Add(card);
                    }
                    return cards;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting hand: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get the current hand size
        /// </summary>
        public int GetHandSize()
        {
            try
            {
                if (_cache.CardManager == null) return 0;

                // Try to get hand count
                var getHandMethod = _cache.CardManager.GetType().GetMethod("GetNumCardsInHand");
                if (getHandMethod != null)
                {
                    return (int)getHandMethod.Invoke(_cache.CardManager, null);
                }

                // Try GetHand and count
                var getHandCardsMethod = _cache.CardManager.GetType().GetMethod("GetHand");
                if (getHandCardsMethod != null)
                {
                    var hand = getHandCardsMethod.Invoke(_cache.CardManager, null);
                    if (hand != null)
                    {
                        var countProp = hand.GetType().GetProperty("Count");
                        if (countProp != null)
                        {
                            return (int)countProp.GetValue(hand);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting hand size: {ex.Message}");
            }
            return 0;
        }

        /// <summary>
        /// Get the current deck size
        /// </summary>
        public int GetDeckSize()
        {
            try
            {
                if (_cache.CardManager == null)
                {
                    _cache.FindManagers();
                }

                if (_cache.CardManager != null)
                {
                    var cardManagerType = _cache.CardManager.GetType();

                    var getCardsMethod = cardManagerType.GetMethod("GetAllCards", Type.EmptyTypes)
                                      ?? cardManagerType.GetMethod("GetDeck", Type.EmptyTypes);
                    if (getCardsMethod != null)
                    {
                        var cards = getCardsMethod.Invoke(_cache.CardManager, null);
                        if (cards is System.Collections.ICollection collection)
                        {
                            return collection.Count;
                        }
                    }

                    var getCountMethod = cardManagerType.GetMethod("GetDeckCount", Type.EmptyTypes);
                    if (getCountMethod != null)
                    {
                        var result = getCountMethod.Invoke(_cache.CardManager, null);
                        if (result is int count) return count;
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting deck size: {ex.Message}");
            }
            return -1;
        }

        public string GetCardTitle(object cardState)
        {
            try
            {
                if (_cache.GetTitleMethod == null)
                {
                    var type = cardState.GetType();
                    _cache.GetTitleMethod = type.GetMethod("GetTitle");
                }
                var title = _cache.GetTitleMethod?.Invoke(cardState, null) as string ?? "Unknown Card";
                return TextUtilities.StripRichTextTags(title);
            }
            catch
            {
                return "Unknown Card";
            }
        }

        public int GetCardCost(object cardState)
        {
            try
            {
                if (_cache.GetCostMethod == null)
                {
                    var type = cardState.GetType();
                    _cache.GetCostMethod = type.GetMethod("GetCostWithoutAnyModifications");
                }
                var result = _cache.GetCostMethod?.Invoke(cardState, null);
                if (result is int cost) return cost;
            }
            catch { }
            return 0;
        }

        public string GetCardDescription(object cardState)
        {
            try
            {
                var type = cardState.GetType();
                string result = null;

                // Try GetDescription first
                var getDescMethod = type.GetMethod("GetDescription");
                if (getDescMethod != null)
                {
                    var desc = getDescMethod.Invoke(cardState, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                        result = desc;
                }

                // Try GetEffectDescription
                if (result == null)
                {
                    var getEffectDescMethod = type.GetMethod("GetEffectDescription");
                    if (getEffectDescMethod != null)
                    {
                        var desc = getEffectDescMethod.Invoke(cardState, null) as string;
                        if (!string.IsNullOrEmpty(desc))
                            result = desc;
                    }
                }

                // Try getting from CardData
                if (result == null)
                {
                    var getCardDataMethod = type.GetMethod("GetCardDataID") ?? type.GetMethod("GetCardData");
                    if (getCardDataMethod != null)
                    {
                        var cardData = getCardDataMethod.Invoke(cardState, null);
                        if (cardData != null)
                        {
                            var dataType = cardData.GetType();
                            var dataDescMethod = dataType.GetMethod("GetDescription");
                            if (dataDescMethod != null)
                            {
                                var desc = dataDescMethod.Invoke(cardData, null) as string;
                                if (!string.IsNullOrEmpty(desc))
                                    result = desc;
                            }
                        }
                    }
                }

                // Strip rich text tags before returning
                return TextUtilities.StripRichTextTags(result);
            }
            catch { }
            return null;
        }

        public string GetCardType(object cardState)
        {
            try
            {
                var type = cardState.GetType();
                var getCardTypeMethod = type.GetMethod("GetCardType");
                if (getCardTypeMethod != null)
                {
                    var cardType = getCardTypeMethod.Invoke(cardState, null);
                    if (cardType != null)
                    {
                        string typeName = cardType.ToString();
                        // Convert enum value to readable name
                        if (typeName == "Monster") return "Unit";
                        if (typeName == "Spell") return "Spell";
                        if (typeName == "Blight") return "Blight";
                        return typeName;
                    }
                }
            }
            catch { }
            return null;
        }

        public string GetCardClan(object cardState)
        {
            try
            {
                var type = cardState.GetType();

                // Get CardData from CardState
                var getCardDataMethod = type.GetMethod("GetCardDataRead", Type.EmptyTypes)
                                     ?? type.GetMethod("GetCardData", Type.EmptyTypes);
                if (getCardDataMethod == null) return null;

                var cardData = getCardDataMethod.Invoke(cardState, null);
                if (cardData == null) return null;

                var cardDataType = cardData.GetType();

                // Get linkedClass field from CardData
                var linkedClassField = cardDataType.GetField("linkedClass",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (linkedClassField == null) return null;

                var linkedClass = linkedClassField.GetValue(cardData);
                if (linkedClass == null) return null;

                var classType = linkedClass.GetType();

                // Try GetTitle() for localized name
                var getTitleMethod = classType.GetMethod("GetTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    var clanName = getTitleMethod.Invoke(linkedClass, null) as string;
                    if (!string.IsNullOrEmpty(clanName)) return clanName;
                }

                // Fallback to GetName()
                var getNameMethod = classType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    return getNameMethod.Invoke(linkedClass, null) as string;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Extract keyword definitions from a card's description
        /// </summary>
        public string GetCardKeywords(object cardState, string description)
        {
            if (string.IsNullOrEmpty(description)) return null;

            try
            {
                var keywords = new List<string>();

                // Known keywords with their definitions
                var knownKeywords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    // Trigger abilities
                    { "Slay", "Slay: Triggers after dealing a killing blow" },
                    { "Revenge", "Revenge: Triggers when damaged" },
                    { "Strike", "Strike: Triggers when attacking" },
                    { "Extinguish", "Extinguish: Triggers when dying" },
                    { "Summon", "Summon: Triggers when played" },
                    { "Incant", "Incant: Triggers when spell played on floor" },
                    { "Resolve", "Resolve: Triggers after combat" },
                    { "Rally", "Rally: Triggers when unit played on floor" },
                    { "Harvest", "Harvest: Triggers when unit dies on floor" },
                    { "Gorge", "Gorge: Triggers when eating a Morsel" },
                    { "Inspire", "Inspire: Triggers when gaining Echo" },
                    { "Rejuvenate", "Rejuvenate: Triggers when healed" },
                    { "Action", "Action: Triggers at turn start" },
                    { "Hatch", "Hatch: Triggers on death" },
                    { "Hunger", "Hunger: Triggers when Eaten unit summoned" },
                    { "Armored", "Armored: Triggers when Armor added" },
                    // Buffs
                    { "Armor", "Armor: Blocks damage" },
                    { "Rage", "Rage: +2 Attack per stack" },
                    { "Regen", "Regen: Heals each turn" },
                    { "Damage Shield", "Damage Shield: Blocks next damage" },
                    { "Lifesteal", "Lifesteal: Heals for damage dealt" },
                    { "Spikes", "Spikes: Damages attackers" },
                    { "Stealth", "Stealth: Not targeted in combat" },
                    { "Spell Shield", "Spell Shield: Absorbs next spell" },
                    { "Spellshield", "Spellshield: Absorbs next spell" },
                    { "Soul", "Soul: Powers Extinguish ability" },
                    // Debuffs
                    { "Frostbite", "Frostbite: Damages at end of turn" },
                    { "Sap", "Sap: -2 Attack per stack" },
                    { "Dazed", "Dazed: Cannot attack" },
                    { "Rooted", "Rooted: Cannot move floors" },
                    { "Emberdrain", "Emberdrain: Lose Ember at turn start" },
                    { "Heartless", "Heartless: Cannot be healed" },
                    { "Melee Weakness", "Melee Weakness: Extra melee damage" },
                    { "Spell Weakness", "Spell Weakness: Extra spell damage" },
                    { "Reap", "Reap: Damages after combat" },
                    // Unit effects
                    { "Quick", "Quick: Attacks first" },
                    { "Multistrike", "Multistrike: Extra attack" },
                    { "Sweep", "Sweep: Attacks all enemies" },
                    { "Trample", "Trample: Excess damage continues" },
                    { "Burnout", "Burnout: Dies when counter reaches 0" },
                    { "Endless", "Endless: Returns to draw pile" },
                    { "Fragile", "Fragile: Dies if damaged" },
                    { "Immobile", "Immobile: Cannot move" },
                    { "Inert", "Inert: Needs Fuel to attack" },
                    { "Fuel", "Fuel: Allows Inert to attack" },
                    { "Phased", "Phased: Cannot be targeted" },
                    { "Relentless", "Relentless: Attacks until floor cleared" },
                    { "Haste", "Haste: Skips to third floor" },
                    { "Cardless", "Cardless: Not from a card" },
                    { "Buffet", "Buffet: Can be eaten multiple times" },
                    { "Shell", "Shell: Uses Echo, triggers Hatch" },
                    { "Silence", "Silence: Disables triggers" },
                    { "Silenced", "Silenced: Triggers disabled" },
                    { "Purify", "Purify: Removes debuffs" },
                    { "Enchant", "Enchant: Buffs floor allies" },
                    { "Shard", "Shard: Powers Solgard" },
                    { "Eaten", "Eaten: Will be eaten" },
                    // Card effects
                    { "Consume", "Consume: One use per battle" },
                    { "Frozen", "Frozen: Not discarded" },
                    { "Permafrost", "Permafrost: Gains Frozen" },
                    { "Purge", "Purge: Removed from deck" },
                    { "Intrinsic", "Intrinsic: Starts in hand" },
                    { "Holdover", "Holdover: Returns to hand" },
                    { "Etch", "Etch: Upgrades when consumed" },
                    { "Offering", "Offering: Plays if discarded" },
                    { "Reserve", "Reserve: Triggers if kept" },
                    { "Pyrebound", "Pyrebound: Pyre room only" },
                    { "Piercing", "Piercing: Ignores Armor" },
                    { "Magic Power", "Magic Power: Boosts spells" },
                    { "Attuned", "Attuned: 5x Magic Power" },
                    { "Infused", "Infused: Adds Echo" },
                    { "Extract", "Extract: Uses charged echoes" },
                    { "Spellchain", "Spellchain: Creates copy" },
                    { "X Cost", "X Cost: Uses all Ember" },
                    { "Unplayable", "Unplayable: Cannot be played" },
                    // Unit actions
                    { "Ascend", "Ascend: Move up" },
                    { "Descend", "Descend: Move down" },
                    { "Reform", "Reform: Returns unit to hand" },
                    { "Sacrifice", "Sacrifice: Kill unit to play" },
                    { "Cultivate", "Cultivate: Buff weakest unit" },
                    // Enemy effects
                    { "Recover", "Recover: Heals after combat" }
                };

                foreach (var keyword in knownKeywords)
                {
                    // Check if keyword appears in description (as whole word)
                    if (Regex.IsMatch(description,
                        $@"\b{Regex.Escape(keyword.Key)}\b",
                        RegexOptions.IgnoreCase))
                    {
                        if (!keywords.Contains(keyword.Value))
                        {
                            keywords.Add(keyword.Value);
                        }
                    }
                }

                if (keywords.Count > 0)
                {
                    return string.Join(". ", keywords);
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get current energy from PlayerManager (needed for playability check)
        /// </summary>
        private int GetCurrentEnergy()
        {
            if (_cache.PlayerManager == null || _cache.GetEnergyMethod == null)
            {
                _cache.FindManagers();
            }

            try
            {
                var result = _cache.GetEnergyMethod?.Invoke(_cache.PlayerManager, null);
                if (result is int energy) return energy;
            }
            catch { }
            return -1;
        }
    }
}
