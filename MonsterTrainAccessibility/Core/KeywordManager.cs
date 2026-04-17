using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MonsterTrainAccessibility.Core
{
    /// <summary>
    /// Builds a keyword dictionary from game localization data.
    /// Pulls status effects, character triggers, and card traits
    /// from the game's own localization system instead of hardcoding.
    /// </summary>
    public static class KeywordManager
    {
        private static Dictionary<string, string> _keywords;
        private static MethodInfo _localizeMethod;
        private static bool _localizeMethodSearched;
        private static readonly HashSet<string> _announcedKeywords =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns a keyword announcement string, including its description the first
        /// time in the session and just the name on subsequent calls. If no keyword
        /// entry exists, returns the name unchanged.
        /// </summary>
        public static string GetKeywordAnnouncement(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var dict = GetKeywords();
            bool firstTime = _announcedKeywords.Add(name);
            if (firstTime && dict != null && dict.TryGetValue(name, out var full) && !string.IsNullOrEmpty(full))
                return full;
            return name;
        }

        /// <summary>
        /// Reset the per-session "already announced" set. Call on battle entry.
        /// </summary>
        public static void ResetAnnouncedKeywords()
        {
            _announcedKeywords.Clear();
        }

        /// <summary>
        /// Get the keyword dictionary, building it on first access.
        /// </summary>
        public static Dictionary<string, string> GetKeywords()
        {
            if (_keywords == null)
                BuildKeywords();
            return _keywords;
        }

        /// <summary>
        /// Force rebuild of the keyword dictionary (e.g. if first attempt was too early).
        /// </summary>
        public static void Reset()
        {
            _keywords = null;
        }

        private static void BuildKeywords()
        {
            _keywords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            EnsureLocalizeMethod();

            int statusCount = LoadStatusEffectKeywords();
            int triggerCount = LoadTriggerKeywords();
            int traitCount = LoadCardTraitKeywords();
            int effectCount = LoadCardEffectTooltipKeywords();
            int currencyCount = LoadCurrencyKeywords();
            int fallbackCount = LoadFallbackKeywords();

            MonsterTrainAccessibility.LogInfo(
                $"KeywordManager built {_keywords.Count} keywords " +
                $"(status={statusCount}, triggers={triggerCount}, traits={traitCount}, " +
                $"effects={effectCount}, currency={currencyCount}, fallback={fallbackCount})");
        }

        private static int LoadStatusEffectKeywords()
        {
            int count = 0;
            try
            {
                Type semType = FindTypeInAssemblies("StatusEffectManager");
                if (semType == null)
                {
                    MonsterTrainAccessibility.LogWarning("KeywordManager: StatusEffectManager type not found");
                    return 0;
                }

                Func<string, int?> paramIntProvider = GetStatusEffectParamIntProvider();

                var field = semType.GetField("StatusIdToLocalizationExpression",
                    BindingFlags.Public | BindingFlags.Static);
                if (field == null)
                {
                    var prop = semType.GetProperty("StatusIdToLocalizationExpression",
                        BindingFlags.Public | BindingFlags.Static);
                    if (prop != null)
                    {
                        var dict = prop.GetValue(null) as System.Collections.IDictionary;
                        if (dict != null)
                            count = ProcessLocalizationDictionary(dict, "_CardText", "_CardTooltipText", paramIntProvider);
                    }
                }
                else
                {
                    var dict = field.GetValue(null) as System.Collections.IDictionary;
                    if (dict != null)
                        count = ProcessLocalizationDictionary(dict, "_CardText", "_CardTooltipText", paramIntProvider);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"KeywordManager: Error loading status effects: {ex.Message}");
            }
            return count;
        }

        private static int LoadTriggerKeywords()
        {
            int count = 0;
            try
            {
                Type ctdType = FindTypeInAssemblies("CharacterTriggerData");
                if (ctdType == null)
                {
                    MonsterTrainAccessibility.LogWarning("KeywordManager: CharacterTriggerData type not found");
                    return 0;
                }

                var field = ctdType.GetField("TriggerToLocalizationExpression",
                    BindingFlags.Public | BindingFlags.Static);
                if (field == null)
                {
                    var prop = ctdType.GetProperty("TriggerToLocalizationExpression",
                        BindingFlags.Public | BindingFlags.Static);
                    if (prop != null)
                    {
                        var dict = prop.GetValue(null) as System.Collections.IDictionary;
                        if (dict != null)
                            count = ProcessLocalizationDictionary(dict, "_CardText", "_TooltipText", null);
                    }
                }
                else
                {
                    var dict = field.GetValue(null) as System.Collections.IDictionary;
                    if (dict != null)
                        count = ProcessLocalizationDictionary(dict, "_CardText", "_TooltipText", null);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"KeywordManager: Error loading triggers: {ex.Message}");
            }
            return count;
        }

        private static int ProcessLocalizationDictionary(
            System.Collections.IDictionary dict, string nameSuffix, string tooltipSuffix,
            Func<string, int?> paramIntProvider)
        {
            int count = 0;
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                try
                {
                    string prefix = entry.Value as string;
                    if (string.IsNullOrEmpty(prefix)) continue;

                    string statusKey = entry.Key as string;
                    int? paramInt = paramIntProvider?.Invoke(statusKey);

                    string name = TryLocalize(prefix + nameSuffix);
                    string tooltip = paramInt.HasValue
                        ? LocalizationHelper.LocalizeWithInt(prefix + tooltipSuffix, paramInt.Value)
                        : TryLocalize(prefix + tooltipSuffix);

                    if (string.IsNullOrEmpty(name) || name == (prefix + nameSuffix))
                        continue;

                    name = TextUtilities.CleanSpriteTagsForSpeech(name).Trim();

                    if (string.IsNullOrEmpty(name))
                        continue;

                    string value;
                    if (!string.IsNullOrEmpty(tooltip) && tooltip != (prefix + tooltipSuffix))
                    {
                        tooltip = TextUtilities.CleanSpriteTagsForSpeech(tooltip).Trim();
                        value = $"{name}: {tooltip}";
                    }
                    else
                    {
                        value = name;
                    }

                    if (!_keywords.ContainsKey(name))
                    {
                        _keywords[name] = value;
                        count++;
                    }
                }
                catch { }
            }
            return count;
        }

        /// <summary>
        /// Load keywords exposed by card effects via ICardEffectStatuslessTooltip.
        /// Each implementor returns a base key (via GetTooltipBaseKey) that the game
        /// localizes as "{base}_TooltipTitle" / "{base}_TooltipText". We can't easily
        /// instantiate a CardEffectState per type, so we try the likely base keys:
        /// the bare type name and the common direction suffixes (_Up, _Down).
        /// This is how MT2's "Advance" / "Descend" (CardEffectBump_Up/_Down) get
        /// pulled into the keyword dictionary.
        /// </summary>
        private static int LoadCardEffectTooltipKeywords()
        {
            int count = 0;
            try
            {
                Type interfaceType = FindTypeInAssemblies("ICardEffectStatuslessTooltip");
                if (interfaceType == null)
                {
                    MonsterTrainAccessibility.LogWarning("KeywordManager: ICardEffectStatuslessTooltip not found");
                    return 0;
                }

                var effectTypes = new List<Type>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var asmName = assembly.GetName().Name;
                    if (!asmName.Contains("Assembly-CSharp") && !asmName.Contains("Trainworks"))
                        continue;
                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type == null || !type.IsClass || type.IsAbstract) continue;
                            if (interfaceType.IsAssignableFrom(type) && type != interfaceType)
                                effectTypes.Add(type);
                        }
                    }
                    catch { }
                }

                string[] suffixes = { "", "_Up", "_Down" };
                foreach (var effectType in effectTypes)
                {
                    string typeName = effectType.Name;
                    foreach (var suffix in suffixes)
                    {
                        string baseKey = typeName + suffix;
                        string titleKey = baseKey + "_TooltipTitle";
                        string textKey = baseKey + "_TooltipText";

                        string name = TryLocalize(titleKey);
                        if (string.IsNullOrEmpty(name) || name == titleKey) continue;

                        // _TooltipText may embed {0} for dynamic strength. Fill with 0 so
                        // the general tooltip description still formats cleanly.
                        string tooltip = LocalizationHelper.LocalizeWithInt(textKey, 0);
                        if (string.IsNullOrEmpty(tooltip) || tooltip == textKey)
                            tooltip = TryLocalize(textKey);

                        name = TextUtilities.CleanSpriteTagsForSpeech(name).Trim();
                        if (string.IsNullOrEmpty(name)) continue;

                        string value;
                        if (!string.IsNullOrEmpty(tooltip) && tooltip != textKey)
                        {
                            tooltip = TextUtilities.CleanSpriteTagsForSpeech(tooltip).Trim();
                            value = $"{name}: {tooltip}";
                        }
                        else
                        {
                            value = name;
                        }

                        if (!_keywords.ContainsKey(name))
                        {
                            _keywords[name] = value;
                            count++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"KeywordManager: Error loading card effect tooltips: {ex.Message}");
            }
            return count;
        }

        private static int LoadCardTraitKeywords()
        {
            int count = 0;

            var traitNames = new[]
            {
                "CardTraitExhaustState",
                "CardTraitIntrinsicState",
                "CardTraitRetainState",
                "CardTraitSelfPurgeState",
                "CardTraitFreeze",
                "CardTraitPermafrostState",
                "CardTraitUnplayable",
                "CardTraitCopyOnPlay",
                "CardTraitIgnoreArmor",
                "CardTraitCorruptRestricted",
                "CardTraitCorruptState",
                "CardTraitEphemeral",
                "CardTraitInfusion",
                "CardTraitJuice",
                "CardTraitJuice3",
                "CardTraitHeavy",
                "CardTraitMagneticState",
                "CardTraitLevelMonsterState",
                "CardTraitGraftedEquipment",
                "CardTraitReturnToHandEquipment",
                "CardTraitDamageOverflow",
                "CardTraitSpellAffinity",
                "CardTraitTreasure",
                "CardTraitUnpurgable",
                "CardTraitDrawInDeploymentPhase",
                "CardTraitShowCardTargets",
                "CardTraitScalingAddDamage",
                "CardTraitScalingAddStatusEffect",
                "CardTraitScalingBuffDamage",
                "CardTraitScalingHeal",
                "CardTraitScalingReduceCost",
                "CardTraitScalingUpgradeUnitAttack",
                "CardTraitScalingUpgradeUnitHealth",
                "CardTraitScalingUpgradeUnitSize",
                "CardTraitScalingUpgradeUnitStatusEffect",
                "CardTraitScalingAddCards",
                "CardTraitScalingAddEnergy",
                "CardTraitScalingAddDamagePerCard",
                "CardTraitScalingAddEffectApplications",
                "CardTraitScalingRewardGold",
                "CardTraitScalingReturnConsumedCards",
                "CardTraitScalingDrawAdditionalNextTurn",
                "CardTraitScalingHealTrain",
                "CardTraitScalingRemoveStatusEffect",
                "CardTraitScalingAdvanceMoonPhase",
                "CardTraitScalingMagicPowerOnMoonPhase",
            };

            foreach (string traitName in traitNames)
            {
                try
                {
                    string name = TryLocalize(traitName + "_CardText");
                    string tooltip = TryLocalize(traitName + "_TooltipText");

                    if (string.IsNullOrEmpty(tooltip) || tooltip == (traitName + "_TooltipText"))
                        tooltip = TryLocalize(traitName + "_CardTooltipText");

                    if (string.IsNullOrEmpty(name) || name == (traitName + "_CardText"))
                        continue;

                    name = TextUtilities.CleanSpriteTagsForSpeech(name).Trim();
                    if (string.IsNullOrEmpty(name)) continue;

                    string value;
                    if (!string.IsNullOrEmpty(tooltip) &&
                        tooltip != (traitName + "_TooltipText") &&
                        tooltip != (traitName + "_CardTooltipText"))
                    {
                        tooltip = TextUtilities.CleanSpriteTagsForSpeech(tooltip).Trim();
                        value = $"{name}: {tooltip}";
                    }
                    else
                    {
                        value = name;
                    }

                    if (!_keywords.ContainsKey(name))
                    {
                        _keywords[name] = value;
                        count++;
                    }
                }
                catch { }
            }

            return count;
        }

        /// <summary>
        /// Register keywords for sprite-icon currencies/resources (Dragon's Hoard, etc).
        /// These aren't status effects or triggers, so they aren't covered by the other
        /// loaders. We resolve their localized name by scanning I2.Loc terms for the
        /// sprite asset id, then pull a tooltip via FindTerm if one exists.
        /// </summary>
        private static int LoadCurrencyKeywords()
        {
            int count = 0;
            // Sprite asset ids embedded in card text by the game (see CardEffectAdjustDragonsHoard.cs).
            string[] currencySprites = { "DragonsHoard" };

            foreach (var sprite in currencySprites)
            {
                try
                {
                    string localizedName = LocalizationHelper.GetSpriteDisplayName(sprite);
                    if (string.IsNullOrEmpty(localizedName)) continue;
                    localizedName = TextUtilities.StripRichTextTags(localizedName).Trim();
                    if (string.IsNullOrEmpty(localizedName)) continue;

                    string tooltipKey = LocalizationHelper.FindTerm(sprite,
                        "_Description", "_Desc", "_TooltipBody", "_TooltipText", "_Tooltip");
                    string tooltip = !string.IsNullOrEmpty(tooltipKey) ? LocalizationHelper.Localize(tooltipKey) : null;

                    string value;
                    if (!string.IsNullOrEmpty(tooltip))
                    {
                        tooltip = TextUtilities.CleanSpriteTagsForSpeech(tooltip).Trim();

                        // Skip tooltip templates the game fills in with string.Format —
                        // showing raw "{0}", "{1}" placeholders is worse than no tooltip.
                        if (System.Text.RegularExpressions.Regex.IsMatch(tooltip, @"\{\d+\}"))
                            tooltip = null;
                    }

                    // If no real tooltip was found, don't register a bare name entry —
                    // let LoadFallbackKeywords provide a description instead.
                    if (string.IsNullOrEmpty(tooltip) || tooltip == localizedName)
                        continue;

                    value = $"{localizedName}: {tooltip}";

                    if (!_keywords.ContainsKey(localizedName))
                    {
                        _keywords[localizedName] = value;
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    MonsterTrainAccessibility.LogWarning($"KeywordManager: failed to load currency '{sprite}': {ex.Message}");
                }
            }
            return count;
        }

        private static int LoadFallbackKeywords()
        {
            int count = 0;
            var fallbacks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Piercing", "Piercing: Damage ignores Armor and shields" },
                { "Magic Power", "Magic Power: Boosts spell damage and healing" },
                { "Attuned", "Attuned: Multiplies Magic Power effects by 5" },
                { "Doublestack", "Doublestack: Status effect stacks added by this card are doubled" },
                { "Offering", "Offering: Played automatically if discarded" },
                { "Reserve", "Reserve: Triggers if card remains in hand at end of turn" },
                { "Pyrebound", "Pyrebound: Only playable in Pyre Room or floor below" },
                { "X Cost", "X Cost: Spends all remaining Ember, effect scales with amount" },
                { "Unplayable", "Unplayable: This card cannot be played" },
                { "Spellchain", "Spellchain: Creates a copy with +1 cost and Purge" },
                { "Infused", "Infused: Floor gains 1 Echo when played" },
                { "Extract", "Extract: Removes charged echoes when played" },
                { "Advance", "Advance: Card effect that moves the targeted unit up a floor" },
                { "Ascend", "Ascend: Move up a floor to the back" },
                { "Descend", "Descend: Move down a floor to the back" },
                { "Reform", "Reform: Return a defeated friendly unit to hand" },
                { "Sacrifice", "Sacrifice: Kill a friendly unit to play this card" },
                { "Cultivate", "Cultivate: Increase stats of lowest health friendly unit" },
                { "Recover", "Recover: Restores health to friendly units after combat" },
                { "Enchant", "Enchant: Other friendly units on floor gain a bonus" },
                { "Eaten", "Eaten: Will be eaten by front unit after combat" },
                { "Soul", "Soul: Powers Devourer of Death's Extinguish ability" },
                { "Shard", "Shard: Powers Solgard the Martyr's abilities" },
                { "Buffet", "Buffet: Can be eaten multiple times" },
                { "Corrupt", "Corrupt: Playing this card adds corruption to the floor" },
                { "Corrupt Restricted", "Corrupt Restricted: Can only be played when floor has enough corruption" },
                { "Purify", "Purify: Removes corruption from the floor" },
                { "Echo", "Echo: Copies the next spell played on this floor" },
                { "Charged Echo", "Charged Echo: Stored echo charge that copies the next spell played" },
                { "Pyre Lock", "Pyre Lock: Prevents the Pyre from being healed" },
                { "Dragon's Hoard", "Dragon's Hoard: Currency collected during a run that unlocks bonus rewards on the Dragon's Hoard screen" },
                { "Dragons Hoard", "Dragons Hoard: Currency collected during a run that unlocks bonus rewards on the Dragon's Hoard screen" },
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
                // MT2 trigger abilities
                { "Deathwish", "Deathwish: Triggers when another unit on this floor dies" },
                { "Valiant", "Valiant: Triggers when this unit has taken damage and survived" },
                { "Regal", "Regal: Triggers when a Regal card is played" },
                { "Shift", "Shift: Triggers when the moon phase changes" },
                { "Moonlit", "Moonlit: Active during Full Moon phase" },
                { "Moonshade", "Moonshade: Active during New Moon phase" },
                { "Timebomb", "Timebomb: Triggers when countdown reaches zero" },
                { "Reanimated", "Reanimated: Triggers when this unit returns from death" },
                { "Troop Added", "Troop Added: Triggers when a unit joins this floor" },
                { "Troop Removed", "Troop Removed: Triggers when a unit leaves this floor" },
                // Buffs
                { "Armor", "Armor: Blocks damage before health, each point blocks one damage" },
                { "Rage", "Rage: +2 Attack per stack, decreases every turn" },
                { "Regen", "Regen: Restores 1 health per stack at end of turn" },
                { "Damage Shield", "Damage Shield: Nullifies the next source of damage" },
                { "Lifesteal", "Lifesteal: Heals for damage dealt when attacking" },
                { "Spikes", "Spikes: Attackers take 1 damage per stack" },
                { "Stealth", "Stealth: Not targeted in combat, loses 1 stack per turn" },
                { "Spell Shield", "Spell Shield: Absorbs the next damage spell" },
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
                // MT2 status effects
                { "Valor", "Valor: Increases Pyre attack damage" },
                { "Avarice", "Avarice: Gain gold when this enemy is killed" },
                { "Titanskin", "Titanskin: Reduces all damage taken by amount" },
                { "Titanite", "Titanite: Increases all damage dealt by amount" },
                { "Unstable", "Unstable: Explodes for damage to all units on floor when killed" },
                { "Horde", "Horde: Gains stats for each unit on this floor" },
                { "Undying", "Undying: Returns to life after death with stacks reduced" },
                { "Conduit", "Conduit: Links effects between floors" },
                { "Blast Resistant", "Blast Resistant: Reduces damage from spells" },
                { "Equalizer", "Equalizer: Sets attack equal to health" },
                { "Mageblade", "Mageblade: Gains Magic Power as attack" },
                { "Lifelink", "Lifelink: Heals the Pyre when this unit heals" },
                { "Pyrebane", "Pyrebane: Deals bonus damage to the Pyre" },
                { "Duality", "Duality: Effects change based on moon phase" },
                { "Sniper", "Sniper: Targets specific units instead of front" },
                { "Mass", "Mass: Affects all units on the floor" },
                { "Decay", "Decay: Loses health each turn" },
                { "Pyregel", "Pyregel: Shields the Pyre from damage" },
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
                { "Morsel", "Morsel: Small unit that gets eaten by front unit after combat" },
                { "Ephemeral", "Ephemeral: Removed from hand at end of turn" },
                { "Infusion", "Infusion: Gains bonuses from floor enchantments" },
                { "Juice", "Juice: Gains extra effect stacks" },
                { "Heavy", "Heavy: Cannot be moved by effects" },
                { "Treasure", "Treasure: Valuable card worth Dragon's Hoard currency" },
                { "Grafted", "Grafted: Permanently attached equipment" },
                { "Overflow", "Overflow: Excess damage carries over to next target" },
                { "Spell Affinity", "Spell Affinity: Gains bonuses when spells are played" },
                // Unit actions
                { "Advance", "Advance: Card effect that moves the targeted unit up a floor" },
                { "Ascend", "Ascend: Move up a floor to the back" },
                { "Descend", "Descend: Move down a floor to the back" },
                { "Reform", "Reform: Return a defeated friendly unit to hand" },
                { "Sacrifice", "Sacrifice: Kill a friendly unit to play this card" },
                { "Cultivate", "Cultivate: Increase stats of lowest health friendly unit" },
                { "Recover", "Recover: Restores health to friendly units after combat" },
            };

            foreach (var kv in fallbacks)
            {
                if (!_keywords.ContainsKey(kv.Key))
                {
                    _keywords[kv.Key] = kv.Value;
                    count++;
                }
            }

            return count;
        }

        #region Localization Helpers

        private static void EnsureLocalizeMethod()
        {
            if (_localizeMethodSearched) return;
            _localizeMethodSearched = true;

            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var asmName = assembly.GetName().Name;
                    if (!asmName.Contains("Assembly-CSharp") && !asmName.Contains("Trainworks"))
                        continue;

                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (!type.IsClass) continue;

                            var method = type.GetMethod("Localize", BindingFlags.Public | BindingFlags.Static);
                            if (method != null && method.ReturnType == typeof(string))
                            {
                                var parameters = method.GetParameters();
                                if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(string))
                                {
                                    _localizeMethod = method;
                                    return;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"KeywordManager: Error finding Localize method: {ex.Message}");
            }
        }

        /// <summary>
        /// Localize a key using the game's localization system.
        /// </summary>
        public static string TryLocalize(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            EnsureLocalizeMethod();
            if (_localizeMethod == null)
                return null;

            try
            {
                var parameters = _localizeMethod.GetParameters();
                var args = new object[parameters.Length];
                args[0] = key;
                for (int i = 1; i < parameters.Length; i++)
                {
                    args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                }

                var result = _localizeMethod.Invoke(null, args) as string;
                if (!string.IsNullOrEmpty(result) && result != key)
                {
                    // Game returns "KEY>>Some_Key<<" as a sentinel for missing entries.
                    if (result.StartsWith("KEY>>") && result.EndsWith("<<"))
                        return null;
                    return result;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Build a function that returns the base paramInt for a status effect ID,
        /// by walking AllGameManagers.Instance.GetStatusEffectManager().GetStatusEffectDataById(id).GetParamInt()
        /// via reflection. Returns null on lookup failure for a given id, allowing
        /// the caller to fall back to a context-less Localize call.
        /// </summary>
        private static Func<string, int?> GetStatusEffectParamIntProvider()
        {
            try
            {
                Type agmType = FindTypeInAssemblies("AllGameManagers");
                if (agmType == null) return null;

                var instanceProp = agmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                var instance = instanceProp?.GetValue(null);
                if (instance == null) return null;

                var getSemMethod = agmType.GetMethod("GetStatusEffectManager", Type.EmptyTypes);
                var sem = getSemMethod?.Invoke(instance, null);
                if (sem == null) return null;

                var semType = sem.GetType();
                var getDataMethod = semType.GetMethod("GetStatusEffectDataById",
                    new[] { typeof(string), typeof(bool) });
                if (getDataMethod == null)
                    getDataMethod = semType.GetMethod("GetStatusEffectDataById", new[] { typeof(string) });
                if (getDataMethod == null) return null;

                bool wantsBool = getDataMethod.GetParameters().Length == 2;

                return statusId =>
                {
                    if (string.IsNullOrEmpty(statusId)) return null;
                    try
                    {
                        var args = wantsBool ? new object[] { statusId, false } : new object[] { statusId };
                        var data = getDataMethod.Invoke(sem, args);
                        if (data == null) return null;

                        var getParamIntMethod = data.GetType().GetMethod("GetParamInt", Type.EmptyTypes);
                        if (getParamIntMethod == null) return null;

                        var result = getParamIntMethod.Invoke(data, null);
                        if (result is int i) return i;
                    }
                    catch { }
                    return null;
                };
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogWarning($"KeywordManager: paramInt provider unavailable: {ex.Message}");
                return null;
            }
        }

        private static Type FindTypeInAssemblies(string typeName)
        {
            var type = Type.GetType(typeName + ", Assembly-CSharp");
            if (type != null) return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
            }
            return null;
        }

        #endregion
    }
}
