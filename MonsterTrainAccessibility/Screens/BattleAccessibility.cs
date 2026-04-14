using MonsterTrainAccessibility.Battle;
using MonsterTrainAccessibility.Core;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Handles accessibility for the battle/combat screen.
    /// Thin coordinator that delegates to specialized readers.
    /// </summary>
    public class BattleAccessibility
    {
        public bool IsInBattle { get; private set; }

        // Subsystems
        private BattleManagerCache _cache;
        private HandReader _handReader;
        private FloorReader _floorReader;
        private ResourceReader _resourceReader;
        private EnemyReader _enemyReader;

        public BattleAccessibility()
        {
        }

        #region Battle Lifecycle

        /// <summary>
        /// Called when combat begins
        /// </summary>
        public void OnBattleEntered()
        {
            IsInBattle = true;

            // Create cache and readers
            _cache = new BattleManagerCache();
            _cache.FindManagers();

            _handReader = new HandReader(_cache);
            _floorReader = new FloorReader(_cache);
            _resourceReader = new ResourceReader(_cache, _handReader);
            _enemyReader = new EnemyReader(_cache, _floorReader);

            MonsterTrainAccessibility.LogInfo("Battle entered");
            MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Battle started");

            // Announce initial state
            AnnounceResources();
        }

        /// <summary>
        /// Called when combat ends
        /// </summary>
        public void OnBattleExited()
        {
            IsInBattle = false;
            MonsterTrainAccessibility.LogInfo("Battle exited");
        }

        /// <summary>
        /// Called at the start of player's turn
        /// </summary>
        public void OnTurnStarted(int ember, int maxEmber, int cardsDrawn)
        {
            var output = MonsterTrainAccessibility.ScreenReader;
            output?.Speak("Your turn", false);

            // Read actual ember from game
            int actualEmber = _resourceReader?.GetCurrentEnergy() ?? -1;
            if (actualEmber >= 0)
            {
                output?.Queue($"{actualEmber} ember");
            }

            if (cardsDrawn > 0)
            {
                output?.Queue($"Drew {cardsDrawn} cards");
            }
        }

        /// <summary>
        /// Called when player ends their turn
        /// </summary>
        public void OnTurnEnded()
        {
            MonsterTrainAccessibility.ScreenReader?.Speak("End turn. Combat phase.", false);
        }

        /// <summary>
        /// End the player's turn via UI button click or method call
        /// </summary>
        public void EndTurn()
        {
            if (!IsInBattle)
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Not in battle");
                return;
            }

            try
            {
                // Try to find and click the End Turn button in the UI
                var battleHudType = Type.GetType("BattleHud, Assembly-CSharp");
                if (battleHudType == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        battleHudType = assembly.GetType("BattleHud");
                        if (battleHudType != null) break;
                    }
                }

                if (battleHudType != null)
                {
                    var battleHud = GameObject.FindObjectOfType(battleHudType);
                    if (battleHud != null)
                    {
                        var endTurnMethod = battleHudType.GetMethod("EndTurn", Type.EmptyTypes)
                            ?? battleHudType.GetMethod("OnEndTurnPressed", Type.EmptyTypes)
                            ?? battleHudType.GetMethod("OnEndTurnClicked", Type.EmptyTypes);

                        if (endTurnMethod != null)
                        {
                            endTurnMethod.Invoke(battleHud, null);
                            MonsterTrainAccessibility.LogInfo("Ended turn via BattleHud method");
                            return;
                        }

                        var go = (battleHud as Component)?.gameObject;
                        if (go != null)
                        {
                            var buttons = go.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                            foreach (var button in buttons)
                            {
                                var name = button.gameObject.name.ToLower();
                                if (name.Contains("endturn") || name.Contains("end turn") || name.Contains("pass"))
                                {
                                    button.onClick?.Invoke();
                                    MonsterTrainAccessibility.LogInfo($"Clicked end turn button: {button.gameObject.name}");
                                    return;
                                }
                            }
                        }
                    }
                }

                // Fallback: try CombatManager.EndPlayerTurn
                if (_cache?.CombatManager != null)
                {
                    var combatType = _cache.CombatManager.GetType();
                    var endTurnMethod = combatType.GetMethod("EndPlayerTurn", Type.EmptyTypes)
                        ?? combatType.GetMethod("PlayerEndTurn", Type.EmptyTypes)
                        ?? combatType.GetMethod("EndTurn", Type.EmptyTypes);

                    if (endTurnMethod != null)
                    {
                        endTurnMethod.Invoke(_cache.CombatManager, null);
                        MonsterTrainAccessibility.LogInfo("Ended turn via CombatManager");
                        return;
                    }
                    else
                    {
                        var methods = combatType.GetMethods();
                        var turnMethods = new List<string>();
                        foreach (var m in methods)
                        {
                            var n = m.Name.ToLower();
                            if (n.Contains("turn") || n.Contains("end"))
                            {
                                if (!turnMethods.Contains(m.Name))
                                    turnMethods.Add(m.Name);
                            }
                        }
                        MonsterTrainAccessibility.LogInfo($"CombatManager turn-related methods: {string.Join(", ", turnMethods)}");
                    }
                }

                MonsterTrainAccessibility.ScreenReader?.Queue("Could not find end turn button");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error ending turn: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Queue("Error ending turn");
            }
        }

        /// <summary>
        /// Called when battle is won
        /// </summary>
        public void OnBattleWon()
        {
            IsInBattle = false;
            MonsterTrainAccessibility.ScreenReader?.Speak("Victory! Battle won.", false);
        }

        /// <summary>
        /// Called when pyre is destroyed
        /// </summary>
        public void OnBattleLost()
        {
            IsInBattle = false;
            MonsterTrainAccessibility.ScreenReader?.Speak("Defeat. The pyre has been destroyed.", false);
        }

        #endregion

        #region Delegating Methods - Hand

        public void AnnounceHand() => _handReader?.AnnounceHand();

        public int GetDeckSize() => _handReader?.GetDeckSize() ?? -1;

        /// <summary>
        /// Announce cards drawn (with card names)
        /// </summary>
        public void OnCardsDrawn(List<string> cardNames)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceCardDraws.Value)
                return;

            if (cardNames.Count == 1)
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"Drew {cardNames[0]}");
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"Drew: {string.Join(", ", cardNames)}");
            }
        }

        /// <summary>
        /// Announce cards drawn (count only, used when card names aren't available)
        /// </summary>
        public void OnCardsDrawn(int count)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceCardDraws.Value)
                return;

            int handSize = _handReader?.GetHandSize() ?? 0;

            if (count == 1)
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Drew 1 card");
            }
            else if (count > 1)
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"Drew {count} cards");
            }
        }

        public void OnCardPlayed(int cardIndex)
        {
            MonsterTrainAccessibility.ScreenReader?.Queue("Card played");
        }

        public void OnCardDiscarded(string cardName)
        {
            if (!string.IsNullOrEmpty(cardName) && cardName != "Card")
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"Discarded {cardName}");
            }
        }

        public void RefreshHand()
        {
            // Called when hand changes - could trigger re-announcement if desired
        }

        #endregion

        #region Delegating Methods - Floors

        public void AnnounceAllFloors() => _floorReader?.AnnounceAllFloors();

        public int GetSelectedFloor() => _floorReader?.GetSelectedFloor() ?? -1;

        public string GetFloorSummary(int floorNumber) => _floorReader?.GetFloorSummary(floorNumber) ?? "";

        public List<string> GetAllEnemies() => _floorReader?.GetAllEnemies() ?? new List<string>();

        public List<string> GetAllFriendlyUnits() => _floorReader?.GetAllFriendlyUnits() ?? new List<string>();

        public List<string> GetAllUnits() => _floorReader?.GetAllUnits() ?? new List<string>();

        #endregion

        #region Delegating Methods - Resources

        public void AnnounceResources() => _resourceReader?.AnnounceResources();

        public int GetPyreHealth() => _resourceReader?.GetPyreHealth() ?? -1;

        public int GetMaxPyreHealth() => _resourceReader?.GetMaxPyreHealth() ?? -1;

        #endregion

        #region Delegating Methods - Enemies

        public void AnnounceEnemies() => _enemyReader?.AnnounceEnemies();

        /// <summary>
        /// Get a brief description of a unit for targeting announcements.
        /// </summary>
        public string GetTargetUnitDescription(object characterState)
        {
            if (_cache == null) return null;
            return UnitInfoHelper.GetTargetUnitDescription(characterState, _cache);
        }

        /// <summary>
        /// Get a detailed description of a unit including stats, status effects, and intent.
        /// </summary>
        public string GetDetailedUnitDescription(object unit)
        {
            if (_cache == null) return null;
            return UnitInfoHelper.GetDetailedUnitDescription(unit, _cache);
        }

        #endregion

        #region Combat Events

        public void OnDamageDealt(string sourceName, string targetName, int damage)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceDamage.Value)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"{sourceName} deals {damage} to {targetName}");
        }

        public void OnUnitDied(string unitName, bool isEnemy, int userFloor = -1)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceDeaths.Value)
                return;

            string prefix = isEnemy ? "Enemy" : "Your";
            string floorInfo = userFloor > 0 ? $" on floor {userFloor}" : "";
            MonsterTrainAccessibility.ScreenReader?.Queue($"{prefix} {unitName} died{floorInfo}");
        }

        public void OnStatusEffectApplied(string unitName, string effectName, int stacks)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceStatusEffects.Value)
                return;

            // Special-case ability cooldown / readiness for clearer output
            string lower = effectName?.ToLowerInvariant() ?? "";
            if (lower == "unit ability available")
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName} ability ready");
                return;
            }
            if (lower == "cooldown")
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName} ability on cooldown {stacks}");
                return;
            }

            MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName} gains {effectName} {stacks}");
        }

        public void OnUnitSpawned(string unitName, bool isEnemy, int floorIndex)
        {
            if (!IsInBattle)
                return;

            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceSpawns.Value)
                return;

            if (string.IsNullOrEmpty(unitName) || unitName == "Unit")
                return;

            string floorName;
            if (floorIndex <= 0)
            {
                floorName = floorIndex == 0 ? "pyre room" : "the battlefield";
            }
            else
            {
                floorName = $"floor {floorIndex}";
            }

            if (isEnemy)
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"Enemy {unitName} enters on {floorName}");
            }
            else
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName} summoned on {floorName}");
            }
        }

        public void OnEnemiesAscended()
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue("Enemies ascend");
        }

        public void OnPyreDamaged(int damage, int remainingHP)
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"Pyre takes {damage} damage! {remainingHP} health remaining");
        }

        public void OnEnemyDialogue(string text)
        {
            if (!IsInBattle)
                return;

            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceDialogue.Value)
                return;

            if (!string.IsNullOrEmpty(text))
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"Enemy says: {text}");
            }
        }

        public void OnCombatResolutionStarted()
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue("Combat!");
        }

        public void OnRelicTriggered(string relicName)
        {
            if (!IsInBattle)
                return;

            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceRelicTriggers.Value)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"{relicName} triggered");
        }

        public void OnCardExhausted(string cardName)
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"{cardName} consumed");
        }

        public void OnPyreHealed(int amount, int currentHP)
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"Pyre healed for {amount}. {currentHP} health");
        }

        public void OnStatusEffectRemoved(string unitName, string effectName, int stacks)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceStatusEffects.Value)
                return;

            // Special-case ability cooldown / readiness
            string lower = effectName?.ToLowerInvariant() ?? "";
            if (lower == "unit ability available")
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName} ability used");
                return;
            }
            if (lower == "cooldown")
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName} ability ready");
                return;
            }

            string message = stacks > 1
                ? $"{unitName} loses {stacks} {effectName}"
                : $"{unitName} loses {effectName}";
            MonsterTrainAccessibility.ScreenReader?.Queue(message);
        }

        public void OnCharacterMoved(string unitName, bool ascended, int destinationFloor)
        {
            if (!IsInBattle) return;
            string verb = ascended ? "ascends" : "descends";
            string where = null;
            if (destinationFloor == 0) where = "Pyre";
            else if (destinationFloor >= 1 && destinationFloor <= 3) where = $"floor {destinationFloor}";
            string msg = where != null ? $"{unitName} {verb} to {where}" : $"{unitName} {verb}";
            MonsterTrainAccessibility.ScreenReader?.Queue(msg);
        }

        public void OnEquipmentAdded(string unitName, string equipmentName)
        {
            if (!IsInBattle) return;
            MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName} equipped {equipmentName}");
        }

        public void OnEquipmentRemoved(string unitName, string equipmentName)
        {
            if (!IsInBattle) return;
            MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName} unequipped {equipmentName}");
        }

        public void OnMoonPhaseChanged(string phaseName)
        {
            if (!IsInBattle) return;
            MonsterTrainAccessibility.ScreenReader?.Queue($"Moon phase: {phaseName}");
        }

        public void OnAllEnemiesDefeated()
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue("All enemies defeated");
        }

        public void OnCombatPhaseChanged(string phaseName)
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue(phaseName);
        }

        public void OnMaxHPBuffed(string unitName, int amount)
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName} gains {amount} max health");
        }

        public void OnAttackDebuffed(string unitName, int amount)
        {
            if (!IsInBattle || amount <= 0) return;
            OnAttackBuffed(unitName, -amount);
        }

        public void OnMaxHPDebuffed(string unitName, int amount)
        {
            if (!IsInBattle || amount <= 0) return;
            MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName} loses {amount} max health");
        }

        public void OnAttackBuffed(string unitName, int amount)
        {
            if (!IsInBattle)
                return;

            if (amount == 0) return;

            // Mirror the game's own notification: CardEffectBuffDamage_Activated formatted
            // with TextFormat_Add / TextFormat_Default. Produces "+X ATK" / "Gana X de ataque"
            // in whatever language the game is running.
            string template = Utilities.LocalizationHelper.Localize("CardEffectBuffDamage_Activated");
            string amountTemplate = Utilities.LocalizationHelper.Localize(amount >= 0 ? "TextFormat_Add" : "TextFormat_Default");
            string announcement;
            if (!string.IsNullOrEmpty(template) && !string.IsNullOrEmpty(amountTemplate))
            {
                try
                {
                    announcement = $"{unitName} {string.Format(template, string.Format(amountTemplate, amount))}";
                }
                catch
                {
                    announcement = amount > 0
                        ? $"{unitName} gains {amount} attack"
                        : $"{unitName} loses {-amount} attack";
                }
            }
            else
            {
                announcement = amount > 0
                    ? $"{unitName} gains {amount} attack"
                    : $"{unitName} loses {-amount} attack";
            }

            // The game's CardEffectBuffDamage_Activated template embeds a literal
            // <sprite name="Attack"> tag — clean it up so it reads as "gains N attack".
            announcement = Utilities.TextUtilities.CleanSpriteTagsForSpeech(announcement);
            MonsterTrainAccessibility.ScreenReader?.Queue(announcement);
        }

        public void OnTriggerAbilityFired(string unitName, string triggerName)
        {
            if (!IsInBattle)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName}'s {triggerName} triggered");
        }

        public void OnWaveStarted(int waveNumber)
        {
            if (!IsInBattle)
                return;

            if (waveNumber > 0)
                MonsterTrainAccessibility.ScreenReader?.Queue($"Enemy wave {waveNumber}");
            else
                MonsterTrainAccessibility.ScreenReader?.Queue("New enemy wave");
        }

        #endregion

        #region Utility

        /// <summary>
        /// Strip rich text tags from text for screen reader output.
        /// Removes Unity rich text tags like <nobr>, <color>, <upgradeHighlight>, etc.
        /// This static method is called from many files across the codebase.
        /// </summary>
        [System.Obsolete("Use TextUtilities.StripRichTextTags instead")]
        public static string StripRichTextTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Use regex to strip all XML-like tags
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "");

            // Also clean up any double spaces that might result
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

            return text.Trim();
        }

        #endregion
    }
}
