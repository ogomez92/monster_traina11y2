using HarmonyLib;
using System;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect battle end (victory/defeat).
    /// CombatManager.StopCombat(bool combatWon) is the actual method - IEnumerator.
    /// We use prefix to capture the combatWon parameter before the coroutine runs.
    /// </summary>
    public static class BattleVictoryPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var combatType = AccessTools.TypeByName("CombatManager");
                if (combatType != null)
                {
                    var method = AccessTools.Method(combatType, "StopCombat");
                    if (method != null)
                    {
                        var prefix = new HarmonyMethod(typeof(BattleVictoryPatch).GetMethod(nameof(Prefix)));
                        harmony.Patch(method, prefix: prefix);
                        MonsterTrainAccessibility.LogInfo("Patched CombatManager.StopCombat");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch StopCombat: {ex.Message}");
            }
        }

        // StopCombat is IEnumerator so we use prefix. __0 = bool combatWon
        public static void Prefix(bool __0)
        {
            try
            {
                if (__0)
                {
                    MonsterTrainAccessibility.BattleHandler?.OnBattleWon();
                }
                else
                {
                    MonsterTrainAccessibility.BattleHandler?.OnBattleLost();
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in battle end patch: {ex.Message}");
            }
        }
    }
}
