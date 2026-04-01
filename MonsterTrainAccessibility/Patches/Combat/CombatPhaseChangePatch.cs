using HarmonyLib;
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
                        announcement = "Boss action";
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
    }
}
