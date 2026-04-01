using HarmonyLib;
using System;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect when enemies ascend floors
    /// </summary>
    public static class EnemyAscendPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try to find the ascend method on CombatManager or HeroManager
                var combatType = AccessTools.TypeByName("CombatManager");
                if (combatType != null)
                {
                    // Try various method names that might handle ascension
                    var method = AccessTools.Method(combatType, "AscendEnemies") ??
                                 AccessTools.Method(combatType, "MoveEnemies") ??
                                 AccessTools.Method(combatType, "ProcessAscend");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(EnemyAscendPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched ascend method: {method.Name}");
                    }
                    else
                    {
                        // Expected in some game versions - ascend is announced via other means
                        MonsterTrainAccessibility.LogInfo("Ascend method not found - will use alternative detection");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch enemy ascend: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.BattleHandler?.OnEnemiesAscended();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ascend patch: {ex.Message}");
            }
        }
    }
}
