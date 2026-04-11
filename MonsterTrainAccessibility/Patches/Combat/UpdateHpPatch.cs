using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using MonsterTrainAccessibility.Patches;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Patch CharacterState.UpdateHp (private, non-coroutine) to announce damage and death
    /// with correct HP values. This fires AFTER the HP is actually changed, unlike
    /// ApplyDamage/ApplyDamageToTarget which are coroutines where postfixes fire too early.
    /// </summary>
    public static class UpdateHpPatch
    {
        // Store old HP per character for computing damage in postfix
        private static Dictionary<int, int> _preHpTracker = new Dictionary<int, int>();
        // Dedup: track last announcement to avoid repeats
        private static string _lastAnnounceKey = "";
        private static float _lastAnnounceTime = 0f;
        // Track recently announced deaths to avoid duplicates with UnitDeathPatch
        public static Dictionary<int, float> RecentDeaths = new Dictionary<int, float>();
        // Cached reflection for attacker lookup
        private static MethodInfo _getLastAttackerMethod;
        private static bool _reflectionCached;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType == null) return;

                // UpdateHp is private void UpdateHp(int newAmount)
                var method = AccessTools.Method(characterType, "UpdateHp", new Type[] { typeof(int) });
                if (method != null)
                {
                    var prefix = new HarmonyMethod(typeof(UpdateHpPatch).GetMethod(nameof(Prefix)));
                    var postfix = new HarmonyMethod(typeof(UpdateHpPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, prefix: prefix, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched CharacterState.UpdateHp for accurate HP tracking");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("CharacterState.UpdateHp not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch UpdateHp: {ex.Message}");
            }
        }

        public static void Prefix(object __instance, int __0)
        {
            try
            {
                if (__instance == null) return;
                int hash = __instance.GetHashCode();
                int currentHP = CharacterStateHelper.GetCurrentHP(__instance);
                _preHpTracker[hash] = currentHP;
            }
            catch { }
        }

        public static void Postfix(object __instance, int __0)
        {
            try
            {
                if (__instance == null) return;

                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                    return;

                if (MonsterTrainAccessibility.BattleHandler == null || !MonsterTrainAccessibility.BattleHandler.IsInBattle)
                    return;

                int hash = __instance.GetHashCode();
                int newHP = __0; // The parameter passed to UpdateHp is the new HP value

                if (!_preHpTracker.TryGetValue(hash, out int oldHP))
                    return;

                if (oldHP == newHP)
                    return;

                // HP decreased = damage
                if (newHP < oldHP)
                {
                    int damage = oldHP - newHP;
                    string targetName = CharacterStateHelper.GetUnitName(__instance);
                    bool isEnemy = CharacterStateHelper.IsEnemyUnit(__instance);

                    string attackerName = GetLastAttackerName(__instance);

                    // Dedup check
                    float currentTime = UnityEngine.Time.unscaledTime;
                    string announceKey = $"{attackerName}_{targetName}_{damage}_{newHP}";
                    if (announceKey == _lastAnnounceKey && currentTime - _lastAnnounceTime < 0.3f)
                        return;
                    _lastAnnounceKey = announceKey;
                    _lastAnnounceTime = currentTime;

                    // Floor prefix: room 0 = floor 3, room 2 = floor 1, room 3 = pyre
                    int roomIdx = CharacterStateHelper.GetRoomIndex(__instance);
                    string floorPrefix = "";
                    if (roomIdx >= 0 && roomIdx <= 2)
                    {
                        int userFloor = 3 - roomIdx;
                        floorPrefix = $"Floor {userFloor}: ";
                    }
                    else if (roomIdx == 3)
                    {
                        floorPrefix = "Pyre room: ";
                    }

                    // Build announcement
                    string announcement;
                    if (!string.IsNullOrEmpty(attackerName) && attackerName != "Unit")
                    {
                        announcement = $"{floorPrefix}{attackerName} hits {targetName} for {damage}";
                    }
                    else
                    {
                        announcement = $"{floorPrefix}{targetName} takes {damage} damage";
                    }

                    if (newHP > 0)
                    {
                        announcement += $", {newHP} HP left";
                    }

                    MonsterTrainAccessibility.ScreenReader?.Speak(announcement);

                    // Death detection
                    if (newHP <= 0)
                    {
                        int roomIndex = CharacterStateHelper.GetRoomIndex(__instance);
                        RecentDeaths[hash] = currentTime;
                        MonsterTrainAccessibility.BattleHandler?.OnUnitDied(targetName, isEnemy, roomIndex);

                        // Clean up old death entries
                        var keysToRemove = new List<int>();
                        foreach (var kv in RecentDeaths)
                        {
                            if (currentTime - kv.Value > 2f)
                                keysToRemove.Add(kv.Key);
                        }
                        foreach (var key in keysToRemove)
                            RecentDeaths.Remove(key);
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in UpdateHp patch: {ex.Message}");
            }
        }

        private static string GetLastAttackerName(object characterState)
        {
            try
            {
                if (!_reflectionCached)
                {
                    _reflectionCached = true;
                    var type = characterState.GetType();
                    _getLastAttackerMethod = type.GetMethod("GetLastAttackerCharacter", Type.EmptyTypes);
                }

                if (_getLastAttackerMethod != null)
                {
                    var attacker = _getLastAttackerMethod.Invoke(characterState, null);
                    if (attacker != null)
                    {
                        return CharacterStateHelper.GetUnitName(attacker);
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
