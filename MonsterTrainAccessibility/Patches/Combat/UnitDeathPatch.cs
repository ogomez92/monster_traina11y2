using HarmonyLib;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect unit death via CharacterState.InnerCharacterDeath(bool death, ...)
    /// Death is also detected in CharacterDamagePatch/UpdateHpPatch when HP <= 0
    /// InnerCharacterDeath signature: (bool death, List&lt;CharacterState&gt; previewIgnoreCharacters, bool ignoreTriggers)
    /// </summary>
    public static class UnitDeathPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var characterType = AccessTools.TypeByName("CharacterState");
                if (characterType != null)
                {
                    // InnerCharacterDeath is the actual death method in Monster Train 2
                    var method = AccessTools.Method(characterType, "InnerCharacterDeath");
                    if (method != null)
                    {
                        var prefix = new HarmonyMethod(typeof(UnitDeathPatch).GetMethod(nameof(Prefix)));
                        harmony.Patch(method, prefix: prefix);
                        MonsterTrainAccessibility.LogInfo("Patched CharacterState.InnerCharacterDeath");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("InnerCharacterDeath not found - death detected via HP tracking");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch InnerCharacterDeath: {ex.Message}");
            }
        }

        // Use prefix since InnerCharacterDeath is IEnumerator (coroutine).
        // __instance is CharacterState, __0 is bool death, __1 is List<CharacterState>, __2 is bool ignoreTriggers
        public static void Prefix(object __instance, bool __0)
        {
            try
            {
                // __0 is the 'death' parameter - if false, this isn't an actual death
                if (!__0) return;

                // Skip if we're in preview mode (preview death, not actual death)
                if (PreviewModeDetector.ShouldSuppressAnnouncement(__instance))
                {
                    return;
                }

                // Only announce death if this target was recently damaged
                // This filters out preview deaths (where InnerCharacterDeath is called during card preview)
                int targetHash = __instance.GetHashCode();
                float currentTime = UnityEngine.Time.unscaledTime;

                if (!DamageAppliedPatch.RecentlyDamagedTargets.TryGetValue(targetHash, out float damageTime))
                {
                    // This target wasn't recently damaged - probably a preview
                    return;
                }

                // Remove from tracking since we're handling the death
                DamageAppliedPatch.RecentlyDamagedTargets.Remove(targetHash);

                // Only announce if death happened within 1 second of damage
                if (currentTime - damageTime > 1f)
                {
                    return;
                }

                // Check if UpdateHpPatch already announced this death
                if (UpdateHpPatch.RecentDeaths.TryGetValue(targetHash, out float deathTime) && currentTime - deathTime < 1f)
                {
                    return;
                }

                string unitName = GetUnitName(__instance);
                bool isEnemy = IsEnemyUnit(__instance);
                int roomIndex = GetRoomIndex(__instance);
                int userFloor = RoomIndexToUserFloor(roomIndex);

                MonsterTrainAccessibility.LogInfo($"Unit died (via death patch): {unitName} (enemy={isEnemy}) on floor {userFloor}");
                MonsterTrainAccessibility.BattleHandler?.OnUnitDied(unitName, isEnemy, userFloor);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in death patch: {ex.Message}");
            }
        }

        private static int GetCurrentHP(object characterState)
        {
            try
            {
                var type = characterState.GetType();
                var getHPMethod = type.GetMethod("GetHP");
                if (getHPMethod != null)
                {
                    var result = getHPMethod.Invoke(characterState, null);
                    if (result is int hp)
                        return hp;
                }
            }
            catch { }
            return -1; // Unknown HP, treat as potentially dead
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
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }

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
                    return team?.ToString() == "Heroes";
                }
            }
            catch { }
            return false;
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
            return roomIndex + 1;
        }
    }
}
