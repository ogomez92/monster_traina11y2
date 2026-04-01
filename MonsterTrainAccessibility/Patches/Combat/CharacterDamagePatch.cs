using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect combat damage via CharacterState.ApplyDamage
    /// This catches melee combat damage that doesn't go through CombatManager.ApplyDamageToTarget
    /// Signature: ApplyDamage(int damage, ApplyDamageParams damageParams)
    /// </summary>
    public static class CharacterDamagePatch
    {
        private static float _lastDamageTime = 0f;
        private static string _lastDamageKey = "";

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType == null) return;

                // List all ApplyDamage overloads for debugging
                var allMethods = characterType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "ApplyDamage")
                    .ToList();

                MonsterTrainAccessibility.LogInfo($"Found {allMethods.Count} ApplyDamage overloads:");
                foreach (var m in allMethods)
                {
                    var ps = m.GetParameters();
                    var sig = string.Join(", ", ps.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    MonsterTrainAccessibility.LogInfo($"  ApplyDamage({sig})");
                }

                // ApplyDamageParams is nested: CharacterState+ApplyDamageParams
                var applyDamageParamsType = AccessTools.TypeByName("CharacterState+ApplyDamageParams")
                    ?? characterType.GetNestedType("ApplyDamageParams", BindingFlags.Public | BindingFlags.NonPublic);

                // Try to find the overload - in MT2 it's (int, ApplyDamageParams, PlayerManager, CardStatistics)
                MethodInfo method = null;
                if (applyDamageParamsType != null)
                {
                    // Find the overload with ApplyDamageParams as second parameter
                    method = allMethods.FirstOrDefault(m =>
                    {
                        var ps = m.GetParameters();
                        return ps.Length >= 2 && ps[0].ParameterType == typeof(int) && ps[1].ParameterType == applyDamageParamsType;
                    });
                }

                // Fallback: try to find any ApplyDamage with an int first parameter
                if (method == null)
                {
                    method = allMethods.FirstOrDefault(m =>
                    {
                        var ps = m.GetParameters();
                        return ps.Length >= 1 && ps[0].ParameterType == typeof(int);
                    });
                }

                // Last fallback: use AccessTools
                if (method == null)
                {
                    method = AccessTools.Method(characterType, "ApplyDamage");
                }

                if (method != null)
                {
                    var parameters = method.GetParameters();
                    MonsterTrainAccessibility.LogInfo($"Patching ApplyDamage with {parameters.Length} parameters:");
                    foreach (var p in parameters)
                    {
                        MonsterTrainAccessibility.LogInfo($"  {p.Name}: {p.ParameterType.Name}");
                    }

                    var prefix = new HarmonyMethod(typeof(CharacterDamagePatch).GetMethod(nameof(Prefix)));
                    var postfix = new HarmonyMethod(typeof(CharacterDamagePatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, prefix: prefix, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched CharacterState.ApplyDamage for combat damage (with preview filter)");
                }
                else
                {
                    MonsterTrainAccessibility.LogWarning("Could not find suitable ApplyDamage method to patch");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CharacterState.ApplyDamage: {ex.Message}");
            }
        }

        // Track HP before damage to detect if it's a preview (preview doesn't change HP)
        private static Dictionary<int, int> _preHpTracker = new Dictionary<int, int>();

        // PREFIX: Record HP before damage is applied
        public static void Prefix(object __instance)
        {
            try
            {
                if (__instance == null) return;
                int hash = __instance.GetHashCode();
                int hp = GetCurrentHP(__instance);
                _preHpTracker[hash] = hp;
            }
            catch { }
        }

        // POSTFIX: Announce combat damage
        // __instance is the CharacterState receiving damage, __0 is damage amount, __1 is ApplyDamageParams
        public static void Postfix(object __instance, int __0, object __1)
        {
            try
            {
                // Skip if we're in preview mode (damage preview, not actual damage)
                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                {
                    return;
                }

                int damage = __0;
                object target = __instance;
                object damageParams = __1;

                if (damage <= 0 || target == null) return;

                // Check for preview mode in damage params
                if (damageParams != null)
                {
                    var paramsType = damageParams.GetType();

                    // Look for preview/simulation flags
                    var previewField = paramsType.GetField("isPreview", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                                       paramsType.GetField("preview", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                                       paramsType.GetField("isSimulation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (previewField != null)
                    {
                        var isPreview = previewField.GetValue(damageParams);
                        if (isPreview is bool preview && preview)
                        {
                            return; // Skip preview damage
                        }
                    }
                }

                int currentHP = GetCurrentHP(target);
                MonsterTrainAccessibility.LogInfo($"CharacterDamagePatch.Postfix: damage={damage}, HP={currentHP}");
                string targetName = GetUnitName(target);
                bool isTargetEnemy = IsEnemyUnit(target);

                // Try to get attacker name from damageParams
                string attackerName = null;
                if (damageParams != null)
                {
                    attackerName = GetAttackerName(damageParams);
                }

                // Create a key to prevent duplicate announcements
                string damageKey = $"{attackerName}_{targetName}_{damage}_{currentHP}";
                float currentTime = UnityEngine.Time.unscaledTime;

                if (damageKey != _lastDamageKey || currentTime - _lastDamageTime > 0.2f)
                {
                    _lastDamageKey = damageKey;
                    _lastDamageTime = currentTime;

                    // Get the floor where combat is happening
                    int floor = GetUnitFloor(target);
                    string floorStr = floor > 0 ? $"Floor {floor}: " : "";

                    MonsterTrainAccessibility.LogInfo($"Combat damage: {attackerName ?? "Unknown"} deals {damage} to {targetName} (enemy={isTargetEnemy}), HP now {currentHP}, floor={floor}");

                    // Build the announcement with floor context
                    string announcement;
                    if (!string.IsNullOrEmpty(attackerName) && attackerName != "Unknown")
                    {
                        announcement = $"{floorStr}{attackerName} hits {targetName} for {damage}";
                    }
                    else
                    {
                        announcement = $"{floorStr}{targetName} takes {damage} damage";
                    }

                    // Add HP info for friendly units
                    if (!isTargetEnemy && currentHP > 0)
                    {
                        announcement += $", {currentHP} HP left";
                    }

                    MonsterTrainAccessibility.ScreenReader?.Queue(announcement);

                    // Check for death
                    if (currentHP <= 0)
                    {
                        int roomIndex = GetRoomIndex(target);
                        int userFloor = RoomIndexToUserFloor(roomIndex);
                        MonsterTrainAccessibility.BattleHandler?.OnUnitDied(targetName, isTargetEnemy, userFloor);
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CharacterState.ApplyDamage patch: {ex.Message}");
            }
        }

        private static string GetAttackerName(object damageParams)
        {
            try
            {
                var paramsType = damageParams.GetType();

                // Try to get the attacker field
                var attackerField = paramsType.GetField("attacker", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (attackerField != null)
                {
                    var attacker = attackerField.GetValue(damageParams);
                    if (attacker != null)
                    {
                        return GetUnitName(attacker);
                    }
                }
            }
            catch { }
            return null;
        }

        private static string GetUnitName(object characterState)
        {
            try
            {
                var type = characterState.GetType();
                var getNameMethod = type.GetMethod("GetName");
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(characterState, null) as string;
                    if (!string.IsNullOrEmpty(name) && !name.Contains("KEY>"))
                        return name;
                }
            }
            catch { }
            return "Unit";
        }

        private static bool IsEnemyUnit(object characterState)
        {
            try
            {
                var getTeamMethod = characterState.GetType().GetMethod("GetTeamType");
                if (getTeamMethod != null)
                {
                    var team = getTeamMethod.Invoke(characterState, null);
                    return team?.ToString() == "Heroes";
                }
            }
            catch { }
            return false;
        }

        private static int GetCurrentHP(object characterState)
        {
            try
            {
                var getHPMethod = characterState.GetType().GetMethod("GetHP");
                if (getHPMethod != null)
                {
                    var result = getHPMethod.Invoke(characterState, null);
                    if (result is int hp)
                        return hp;
                }
            }
            catch { }
            return -1;
        }

        private static int GetRoomIndex(object characterState)
        {
            try
            {
                var type = characterState.GetType();
                var getRoomMethod = type.GetMethod("GetCurrentRoomIndex");
                if (getRoomMethod != null)
                {
                    var result = getRoomMethod.Invoke(characterState, null);
                    if (result is int index)
                        return index;
                }
            }
            catch { }
            return -1;
        }

        private static int RoomIndexToUserFloor(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex > 2) return -1;
            return 3 - roomIndex;
        }

        private static int GetUnitFloor(object characterState)
        {
            int roomIndex = GetRoomIndex(characterState);
            return RoomIndexToUserFloor(roomIndex);
        }
    }
}
