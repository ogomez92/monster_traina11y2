using HarmonyLib;
using MonsterTrainAccessibility.Utilities;
using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect combat phase transitions for granular announcements.
    /// Hooks CombatManager.SetCombatPhase(Phase) - private method.
    /// Complements existing CombatPhasePatch by announcing MonsterTurn, HeroTurn, etc.
    /// </summary>
    public static class CombatPhaseChangePatch
    {
        private static string _lastPhase = "";

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var combatType = AccessTools.TypeByName("CombatManager");
                if (combatType == null) return;

                var method = combatType.GetMethod("SetCombatPhase",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(CombatPhaseChangePatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched CombatManager.SetCombatPhase");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch SetCombatPhase: {ex.Message}");
            }
        }

        // __0 = Phase enum value
        public static void Postfix(object __0)
        {
            try
            {
                if (__0 == null) return;
                string phaseName = __0.ToString();

                if (phaseName == _lastPhase) return;
                _lastPhase = phaseName;

                // Map phases to user-friendly names
                string announcement = null;
                switch (phaseName)
                {
                    case "MonsterTurn":
                        announcement = "Your units attack";
                        break;
                    case "HeroTurn":
                        announcement = "Enemy turn";
                        break;
                    case "EndOfCombat":
                        announcement = "Combat ended";
                        break;
                    case "BossActionPreCombat":
                    case "BossActionPostCombat":
                        announcement = BuildBossActionAnnouncement();
                        break;
                }

                if (announcement != null)
                {
                    MonsterTrainAccessibility.BattleHandler?.OnCombatPhaseChanged(announcement);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in combat phase change patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Look up the boss's next queued action via reflection and build an
        /// announcement like "Boss action. {description}. Targeting {floor}."
        /// Falls back to plain "Boss action" if lookup fails.
        /// </summary>
        private static string BuildBossActionAnnouncement()
        {
            const string fallback = "Boss action";
            try
            {
                var agmType = AccessTools.TypeByName("AllGameManagers");
                var instance = agmType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                var heroManager = agmType?.GetMethod("GetHeroManager", Type.EmptyTypes)?.Invoke(instance, null);
                if (heroManager == null) return fallback;

                var bossChar = heroManager.GetType()
                    .GetMethod("GetOuterTrainBossCharacter", Type.EmptyTypes)?.Invoke(heroManager, null);
                if (bossChar == null) return fallback;

                var bossState = bossChar.GetType()
                    .GetMethod("GetBossState", Type.EmptyTypes)?.Invoke(bossChar, null);
                if (bossState == null) return fallback;

                var nextAction = bossState.GetType()
                    .GetMethod("GetNextBossAction", Type.EmptyTypes)?.Invoke(bossState, null);
                if (nextAction == null) return fallback;

                var actionType = nextAction.GetType();
                bool isEmpty = (bool)(actionType.GetMethod("IsEmptyAction", Type.EmptyTypes)?.Invoke(nextAction, null) ?? false);
                if (isEmpty) return fallback;

                string description = actionType.GetMethod("GetTooltipDescription", Type.EmptyTypes)
                    ?.Invoke(nextAction, null) as string;
                description = TextUtilities.CleanSpriteTagsForSpeech(description);
                description = TextUtilities.StripRichTextTags(description)?.Trim();

                int targetRoomIndex = -1;
                var getRoomIdx = actionType.GetMethod("GetTargetedRoomIndex", Type.EmptyTypes);
                if (getRoomIdx != null)
                {
                    var result = getRoomIdx.Invoke(nextAction, null);
                    if (result is int i) targetRoomIndex = i;
                }

                var parts = new System.Collections.Generic.List<string> { fallback };
                if (!string.IsNullOrEmpty(description))
                    parts.Add(description);
                if (targetRoomIndex >= 0 && targetRoomIndex <= 3)
                {
                    int userFloor = targetRoomIndex == 3 ? 0 : targetRoomIndex + 1;
                    parts.Add($"targeting {Battle.FloorReader.GetFloorDisplayName(userFloor)}");
                }

                return string.Join(". ", parts);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"BuildBossActionAnnouncement failed: {ex.Message}");
                return fallback;
            }
        }
    }
}
