using HarmonyLib;
using System;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect combat phase changes for better context
    /// </summary>
    public static class CombatPhasePatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var combatType = AccessTools.TypeByName("CombatManager");
                if (combatType != null)
                {
                    // Try to patch combat resolution (when units attack)
                    var method = AccessTools.Method(combatType, "ProcessCombat") ??
                                 AccessTools.Method(combatType, "ResolveCombat") ??
                                 AccessTools.Method(combatType, "RunCombat");

                    if (method != null)
                    {
                        var prefix = new HarmonyMethod(typeof(CombatPhasePatch).GetMethod(nameof(PrefixCombat)));
                        harmony.Patch(method, prefix: prefix);
                        MonsterTrainAccessibility.LogInfo($"Patched combat resolution: {method.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch combat phase: {ex.Message}");
            }
        }

        public static void PrefixCombat()
        {
            try
            {
                MonsterTrainAccessibility.BattleHandler?.OnCombatResolutionStarted();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in combat phase patch: {ex.Message}");
            }
        }
    }
}
