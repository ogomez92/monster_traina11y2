using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Detect when combat starts
    /// </summary>
    public static class CombatStartPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CombatManager");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "StartCombat");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(CombatStartPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched CombatManager.StartCombat");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CombatManager: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("CombatManager.StartCombat called!");
                ScreenStateTracker.SetScreen(Help.GameScreen.Battle);

                if (MonsterTrainAccessibility.BattleHandler == null)
                {
                    MonsterTrainAccessibility.LogInfo("BattleHandler is null - announcing directly");
                    MonsterTrainAccessibility.ScreenReader?.Speak("Battle started. Press H for hand, L for floors, R for resources, F1 for help.", false);
                }
                else
                {
                    MonsterTrainAccessibility.BattleHandler.OnBattleEntered();
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CombatStart patch: {ex.Message}");
            }
        }
    }
}
