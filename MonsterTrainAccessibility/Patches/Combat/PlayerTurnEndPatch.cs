using HarmonyLib;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect player turn end.
    /// CombatManager has no EndPlayerTurn method. The turn end is detected via CombatPhaseChangePatch
    /// when phase transitions away from MonsterTurn (e.g., to Combat or EndMonsterTurn).
    /// </summary>
    public static class PlayerTurnEndPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            // No direct method to patch - turn end is announced via CombatPhaseChangePatch
            MonsterTrainAccessibility.LogInfo("PlayerTurnEndPatch: Handled by CombatPhaseChangePatch (phase transitions)");
        }
    }
}
