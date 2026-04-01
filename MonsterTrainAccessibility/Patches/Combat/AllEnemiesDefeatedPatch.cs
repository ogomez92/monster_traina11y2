using HarmonyLib;
using System;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect when all enemies on the current wave have been defeated.
    /// CombatManager implements ICharacterNotifications.NoMoreHeroes() which is called
    /// by HeroManager when all heroes (enemies) are dead. It's a public IEnumerator.
    /// </summary>
    public static class AllEnemiesDefeatedPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // NoMoreHeroes is on CombatManager (implements ICharacterNotifications)
                var combatManagerType = AccessTools.TypeByName("CombatManager");
                if (combatManagerType != null)
                {
                    var method = AccessTools.Method(combatManagerType, "NoMoreHeroes");
                    if (method != null)
                    {
                        var prefix = new HarmonyMethod(typeof(AllEnemiesDefeatedPatch).GetMethod(nameof(Prefix)));
                        harmony.Patch(method, prefix: prefix);
                        MonsterTrainAccessibility.LogInfo("Patched CombatManager.NoMoreHeroes for all-enemies-defeated detection");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("CombatManager.NoMoreHeroes not found");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch all enemies defeated: {ex.Message}");
            }
        }

        // NoMoreHeroes is IEnumerator so use prefix
        public static void Prefix()
        {
            try
            {
                MonsterTrainAccessibility.BattleHandler?.OnAllEnemiesDefeated();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in all enemies defeated patch: {ex.Message}");
            }
        }
    }
}
