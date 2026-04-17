using MonsterTrainAccessibility.Battle;
using MonsterTrainAccessibility.Core;
using MonsterTrainAccessibility.Utilities;
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
            KeywordManager.ResetAnnouncedKeywords();

            // Create cache and readers
            _cache = new BattleManagerCache();
            _cache.FindManagers();

            _handReader = new HandReader(_cache);
            _floorReader = new FloorReader(_cache);
            _resourceReader = new ResourceReader(_cache, _handReader);
            _enemyReader = new EnemyReader(_cache, _floorReader);

            // Reset the ability hint so it announces again this battle
            AbilityFocusSystem.Instance?.ResetHint();

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
            // Use deployment phase key if available, otherwise fall back
            string turnMsg = LocalizationHelper.Localize("CombatMsg_DeploymentPhase") ?? "▶";
            output?.Speak(turnMsg, false);

            // Read actual ember from game
            int actualEmber = _resourceReader?.GetCurrentEnergy() ?? -1;
            if (actualEmber >= 0)
            {
                output?.Queue($"{actualEmber} {ModLocalization.Ember}");
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
        /// Activate the Pyre's ability by invoking Hud.ActivatePyreAbility via reflection.
        /// Announces the ability name on press and the reason if activation fails.
        /// </summary>
        public void ActivatePyreAbility()
        {
            if (!IsInBattle)
            {
                MonsterTrainAccessibility.ScreenReader?.Queue("Not in battle");
                return;
            }

            try
            {
                // Find the pyre heart to describe the ability.
                object pyreHeart = null;
                if (_cache?.RoomManager != null)
                {
                    var getPyreRoom = _cache.RoomManager.GetType().GetMethod("GetPyreRoom", Type.EmptyTypes);
                    var pyreRoom = getPyreRoom?.Invoke(_cache.RoomManager, null);
                    if (pyreRoom != null)
                    {
                        var getPyreHeart = pyreRoom.GetType().GetMethod("GetPyreHeart", Type.EmptyTypes);
                        pyreHeart = getPyreHeart?.Invoke(pyreRoom, null);
                    }
                }

                string abilityName = "Pyre ability";
                if (pyreHeart != null)
                {
                    var getAbility = pyreHeart.GetType().GetMethod("GetUnitAbilityCardState", Type.EmptyTypes);
                    var abilityCard = getAbility?.Invoke(pyreHeart, null);
                    if (abilityCard != null)
                    {
                        var getTitle = abilityCard.GetType().GetMethod("GetTitle", Type.EmptyTypes);
                        var title = getTitle?.Invoke(abilityCard, null) as string;
                        if (!string.IsNullOrEmpty(title))
                            abilityName = Utilities.TextUtilities.StripRichTextTags(title);
                    }
                }

                // Find Hud and invoke its private ActivatePyreAbility.
                Type hudType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    hudType = asm.GetType("Hud");
                    if (hudType != null) break;
                }

                if (hudType == null)
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak("Could not find Hud", false);
                    return;
                }

                var hud = GameObject.FindObjectOfType(hudType);
                if (hud == null)
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak("Hud not active", false);
                    return;
                }

                var activateMethod = hudType.GetMethod("ActivatePyreAbility",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);
                if (activateMethod == null)
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak("Pyre ability method not found", false);
                    return;
                }

                var result = activateMethod.Invoke(hud, null);
                bool success = result is bool b && b;
                if (success)
                {
                    MonsterTrainAccessibility.ScreenReader?.Speak($"Activated {abilityName}", false);
                }
                else
                {
                    string reason = "not ready";
                    if (pyreHeart != null)
                    {
                        var canActivate = pyreHeart.GetType().GetMethod("CanActivateUnitAbility", Type.EmptyTypes);
                        var canRes = canActivate?.Invoke(pyreHeart, null);
                        if (canRes is bool cb && !cb) reason = "on cooldown or not player turn";
                    }
                    MonsterTrainAccessibility.ScreenReader?.Speak($"{abilityName} {reason}", false);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"ActivatePyreAbility error: {ex}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Error activating pyre ability", false);
            }
        }

        /// <summary>
        /// Called when battle is won
        /// </summary>
        public void OnBattleWon()
        {
            IsInBattle = false;
            // ScoreEvent_NormalBattleNoDamage is the game's "Battle Won" key
            string victory = LocalizationHelper.Localize("ScoreEvent_NormalBattleNoDamage") ?? "Victory!";
            MonsterTrainAccessibility.ScreenReader?.Speak(TextUtilities.StripRichTextTags(victory), false);
        }

        /// <summary>
        /// Called when pyre is destroyed
        /// </summary>
        public void OnBattleLost()
        {
            IsInBattle = false;
            string defeat = LocalizationHelper.Localize("ScoreEvent_NormalBattleLoss") ?? "Defeat";
            MonsterTrainAccessibility.ScreenReader?.Speak(TextUtilities.StripRichTextTags(defeat), false);
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
                MonsterTrainAccessibility.ScreenReader?.Queue($"Drew {cardNames[0]}");
            else
                MonsterTrainAccessibility.ScreenReader?.Queue($"Drew: {string.Join(", ", cardNames)}");
        }

        /// <summary>
        /// Announce cards drawn (count only, used when card names aren't available)
        /// </summary>
        public void OnCardsDrawn(int count)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceCardDraws.Value)
                return;

            if (count > 0)
            {
                MonsterTrainAccessibility.ScreenReader?.Queue($"Drew {count} cards");
            }
        }

        public void OnCardPlayed(int cardIndex)
        {
            // Card name already announced by card selection; no English needed
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

            string floorInfo = userFloor > 0 ? $" ({Battle.FloorReader.GetFloorDisplayName(userFloor)})" : "";
            string prefix = isEnemy ? "Enemy" : "Your";
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
                MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName}: {effectName}");
                AbilityFocusSystem.Instance?.OnAbilityBecameAvailable();
                return;
            }
            if (lower == "cooldown")
            {
                string cdName = ModLocalization.StatusEffectName("cooldown", stacks);
                MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName}: {cdName} {stacks}");
                return;
            }

            MonsterTrainAccessibility.ScreenReader?.Queue(ModLocalization.Phrase("GainsStacks", unitName, stacks, effectName));
        }

        public void OnUnitSpawned(string unitName, bool isEnemy, int floorIndex)
        {
            if (!IsInBattle) return;

            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceSpawns.Value)
                return;

            if (string.IsNullOrEmpty(unitName) || unitName == "Unit")
                return;

            string floorName = floorIndex >= 0
                ? Battle.FloorReader.GetFloorDisplayName(floorIndex)
                : "";

            if (isEnemy)
                MonsterTrainAccessibility.ScreenReader?.Queue(
                    !string.IsNullOrEmpty(floorName) ? $"Enemy {unitName} enters on {floorName}" : $"Enemy {unitName}");
            else
                MonsterTrainAccessibility.ScreenReader?.Queue(
                    !string.IsNullOrEmpty(floorName) ? $"{unitName} summoned on {floorName}" : $"{unitName} summoned");
        }

        public void OnEnemiesAscended()
        {
            if (!IsInBattle) return;

            MonsterTrainAccessibility.ScreenReader?.Queue("Enemies ascend");
        }

        public void OnPyreDamaged(int damage, int remainingHP)
        {
            if (!IsInBattle) return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"{ModLocalization.Pyre} -{damage} ({remainingHP} HP)");
        }

        public void OnEnemyDialogue(string text)
        {
            if (!IsInBattle)
                return;

            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceDialogue.Value)
                return;

            if (!string.IsNullOrEmpty(text))
            {
                MonsterTrainAccessibility.ScreenReader?.Queue(text);
            }
        }

        public void OnCombatResolutionStarted()
        {
            if (!IsInBattle) return;

            MonsterTrainAccessibility.ScreenReader?.Queue("Combat!");
        }

        public void OnRelicTriggered(string relicName)
        {
            if (!IsInBattle) return;

            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceRelicTriggers.Value)
                return;

            // Relic name is already localized from the game
            MonsterTrainAccessibility.ScreenReader?.Queue($"{relicName} triggered");
        }

        public void OnCardExhausted(string cardName)
        {
            if (!IsInBattle) return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"{cardName} consumed");
        }

        public void OnPyreHealed(int amount, int currentHP)
        {
            if (!IsInBattle) return;

            MonsterTrainAccessibility.ScreenReader?.Queue(ModLocalization.Phrase("PyreHealed", amount, currentHP));
        }

        public void OnStatusEffectRemoved(string unitName, string effectName, int stacks)
        {
            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceStatusEffects.Value)
                return;

            // Special-case ability cooldown / readiness
            string lower = effectName?.ToLowerInvariant() ?? "";
            if (lower == "unit ability available" || lower == "cooldown")
            {
                // These are handled by OnStatusEffectApplied already
                return;
            }

            MonsterTrainAccessibility.ScreenReader?.Queue(
                stacks > 1 ? $"{unitName} -{stacks} {effectName}" : $"{unitName} -{effectName}");
        }

        public void OnCharacterMoved(string unitName, bool ascended, int destinationFloor)
        {
            if (!IsInBattle) return;
            string where = Battle.FloorReader.GetFloorDisplayName(destinationFloor);
            string verb = ascended ? "ascends to" : "descends to";
            MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName} {verb} {where}");
        }

        public void OnEquipmentAdded(string unitName, string equipmentName)
        {
            if (!IsInBattle) return;
            MonsterTrainAccessibility.ScreenReader?.Queue(ModLocalization.Phrase("Equipped", unitName, equipmentName));
        }

        public void OnEquipmentRemoved(string unitName, string equipmentName)
        {
            if (!IsInBattle) return;
            MonsterTrainAccessibility.ScreenReader?.Queue(ModLocalization.Phrase("Unequipped", unitName, equipmentName));
        }

        public void OnMoonPhaseChanged(string phaseName)
        {
            if (!IsInBattle) return;
            MonsterTrainAccessibility.ScreenReader?.Queue(phaseName);
        }

        public void OnAllEnemiesDefeated()
        {
            if (!IsInBattle) return;

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
            if (!IsInBattle) return;

            MonsterTrainAccessibility.ScreenReader?.Queue(ModLocalization.MaxHPBuffed(unitName, amount));
        }

        public void OnAttackDebuffed(string unitName, int amount)
        {
            if (!IsInBattle || amount <= 0) return;
            OnAttackBuffed(unitName, -amount);
        }

        public void OnMaxHPDebuffed(string unitName, int amount)
        {
            if (!IsInBattle || amount <= 0) return;
            MonsterTrainAccessibility.ScreenReader?.Queue(ModLocalization.MaxHPDebuffed(unitName, amount));
        }

        public void OnAttackBuffed(string unitName, int amount)
        {
            if (!IsInBattle || amount == 0) return;

            MonsterTrainAccessibility.ScreenReader?.Queue(ModLocalization.AttackBuffed(unitName, amount));
        }

        public void OnTriggerAbilityFired(string unitName, string triggerName)
        {
            if (!IsInBattle) return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName}: {triggerName}");
        }

        public void OnWaveStarted(int waveNumber)
        {
            if (!IsInBattle) return;

            MonsterTrainAccessibility.ScreenReader?.Queue(ModLocalization.WaveStarted(waveNumber));
        }

        public void OnEnergyModified(int amount, bool everyTurn)
        {
            if (!IsInBattle) return;

            // Use +/- format with localized ember name
            string sign = amount > 0 ? "+" : "";
            MonsterTrainAccessibility.ScreenReader?.Queue($"{sign}{amount} {ModLocalization.Ember}");
        }

        public void OnDrawCountModified(int amount)
        {
            if (!IsInBattle) return;

            string sign = amount > 0 ? "+" : "";
            // Card draw is shown next to the draw pile name
            MonsterTrainAccessibility.ScreenReader?.Queue($"{sign}{amount} {ModLocalization.DrawPileName}");
        }

        public void OnPyreArmorChanged(int armorValue)
        {
            if (!IsInBattle) return;

            string armor = ModLocalization.StatusEffectName("armor", armorValue);
            MonsterTrainAccessibility.ScreenReader?.Queue(
                $"{ModLocalization.Pyre}: {armor} {armorValue}");
        }

        public void OnUnitSacrificed(string unitName, bool isEnemy)
        {
            if (!IsInBattle) return;

            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceDeaths.Value)
                return;

            // "Sacrifice" keyword is already localized in the fallback dictionary
            string sacrifice = LocalizationHelper.Localize("Trigger_OnKill_CharacterTriggerData_CardText")
                ?? "Sacrifice";
            sacrifice = TextUtilities.StripRichTextTags(sacrifice).Trim();
            MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName}: {sacrifice}");
        }

        public void OnHPDebuffed(string unitName, int amount)
        {
            if (!IsInBattle) return;

            if (!MonsterTrainAccessibility.AccessibilitySettings.AnnounceDamage.Value)
                return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName} -{amount} HP");
        }

        public void OnTriggerAdded(string unitName, string triggerName)
        {
            if (!IsInBattle) return;

            MonsterTrainAccessibility.ScreenReader?.Queue(ModLocalization.Phrase("GainsTrigger", unitName, triggerName));
        }

        public void OnTriggerRemoved(string unitName, string triggerName)
        {
            if (!IsInBattle) return;

            MonsterTrainAccessibility.ScreenReader?.Queue(ModLocalization.Phrase("LosesTrigger", unitName, triggerName));
        }

        public void OnCardUpgradeApplied(string unitName, string upgradeSummary)
        {
            if (!IsInBattle) return;

            MonsterTrainAccessibility.ScreenReader?.Queue($"{unitName}: {upgradeSummary}");
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
