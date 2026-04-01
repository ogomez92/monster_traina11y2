using HarmonyLib;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Detect player turn start.
    /// CombatManager has no StartPlayerTurn method. The player's turn is the MonsterTurn phase
    /// (confusing naming). Turn start is handled by CombatPhaseChangePatch when phase == MonsterTurn.
    /// This patch is kept as a stub for backward compatibility with BattleHandler.OnTurnStarted.
    /// </summary>
    public static class PlayerTurnStartPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            // No direct method to patch - turn start is announced via CombatPhaseChangePatch
            // when SetCombatPhase(Phase.MonsterTurn) is called.
            MonsterTrainAccessibility.LogInfo("PlayerTurnStartPatch: Handled by CombatPhaseChangePatch (MonsterTurn phase)");
        }
    }
}
