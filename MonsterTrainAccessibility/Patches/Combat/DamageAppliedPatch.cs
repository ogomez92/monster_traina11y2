using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect damage dealt
    /// Signature: ApplyDamageToTarget(int damage, CharacterState target, ApplyDamageToTargetParameters parameters)
    /// </summary>
    public static class DamageAppliedPatch
    {
        // Track last damage to avoid duplicate announcements
        private static float _lastDamageTime = 0f;
        private static string _lastDamageKey = "";
        // Track HP before damage to filter out previews
        private static Dictionary<int, int> _preHpTracker = new Dictionary<int, int>();

        // Track recently damaged targets for death correlation
        // Key = target hash, Value = time of damage announcement
        public static Dictionary<int, float> RecentlyDamagedTargets = new Dictionary<int, float>();

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var combatType = AccessTools.TypeByName("CombatManager");
                if (combatType != null)
                {
                    // Try different method names that might handle damage
                    var method = AccessTools.Method(combatType, "ApplyDamageToTarget") ??
                                 AccessTools.Method(combatType, "ApplyDamage");

                    if (method != null)
                    {
                        // Log the actual parameters
                        var parameters = method.GetParameters();
                        MonsterTrainAccessibility.LogInfo($"ApplyDamageToTarget has {parameters.Length} parameters:");
                        foreach (var p in parameters)
                        {
                            MonsterTrainAccessibility.LogInfo($"  {p.Name}: {p.ParameterType.Name}");
                        }

                        var prefix = new HarmonyMethod(typeof(DamageAppliedPatch).GetMethod(nameof(Prefix)));
                        var postfix = new HarmonyMethod(typeof(DamageAppliedPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, prefix: prefix, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched damage method: {method.Name} (with preview filter)");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch damage: {ex.Message}");
            }
        }

        // PREFIX: Record HP before damage - __1 is the target CharacterState
        public static void Prefix(object __1)
        {
            try
            {
                if (__1 == null) return;
                int hash = __1.GetHashCode();
                int hp = GetCurrentHP(__1);
                _preHpTracker[hash] = hp;
            }
            catch { }
        }

        // Use positional parameters - ApplyDamageToTarget may take a struct parameter
        public static void Postfix(object __0, object __1, object __2)
        {
            try
            {
                // Skip if we're in preview mode (damage preview, not actual damage)
                if (PreviewModeDetector.ShouldSuppressAnnouncement(__1))
                {
                    return;
                }

                // Try to extract damage and target from the parameters
                int damage = 0;
                object target = null;

                // If __0 is the damage amount (int)
                if (__0 is int dmg)
                {
                    damage = dmg;
                    target = __1;
                }
                // If __0 is a parameters struct, try to extract from it
                else if (__0 != null)
                {
                    var paramsType = __0.GetType();

                    // Try to get damage from struct
                    var damageField = paramsType.GetField("damage", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (damageField != null && damageField.GetValue(__0) is int d)
                        damage = d;

                    // Try to get target from struct
                    var targetField = paramsType.GetField("target", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (targetField != null)
                        target = targetField.GetValue(__0);
                }

                if (damage <= 0 || target == null)
                {
                    return;
                }

                string targetName = GetUnitName(target);
                bool isEnemy = IsEnemyUnit(target);
                float currentTime = UnityEngine.Time.unscaledTime;
                int targetHash = target.GetHashCode();

                // Track this damage call - duplicate filtering using delay
                // If we see the same target+damage within 0.3s, skip (likely duplicate call)
                string damageKey = $"{targetName}_{damage}";

                if (damageKey == _lastDamageKey && currentTime - _lastDamageTime < 0.3f)
                {
                    return;
                }

                _lastDamageKey = damageKey;
                _lastDamageTime = currentTime;

                // Announce damage
                MonsterTrainAccessibility.ScreenReader?.Queue($"{targetName} takes {damage} damage");

                // Record this target as recently damaged for death correlation
                RecentlyDamagedTargets[targetHash] = currentTime;

                // Clean up old entries (older than 2 seconds)
                var keysToRemove = RecentlyDamagedTargets.Where(kv => currentTime - kv.Value > 2f).Select(kv => kv.Key).ToList();
                foreach (var key in keysToRemove)
                    RecentlyDamagedTargets.Remove(key);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in damage patch: {ex.Message}");
            }
        }

        private static string GetUnitName(object characterState)
        {
            try
            {
                var type = characterState.GetType();

                // Try GetName first (localized)
                var getNameMethod = type.GetMethod("GetName");
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(characterState, null) as string;
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }

                // Fallback to GetSourceCharacterData (MT2 uses this instead of GetCharacterDataRead)
                var getDataMethod = type.GetMethod("GetSourceCharacterData") ?? type.GetMethod("GetCharacterData");
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(characterState, null);
                    if (data != null)
                    {
                        var dataGetNameMethod = data.GetType().GetMethod("GetName");
                        if (dataGetNameMethod != null)
                        {
                            var name = dataGetNameMethod.Invoke(data, null) as string;
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
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
                    // Team.Type.Heroes are enemies in Monster Train
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
    }
}
