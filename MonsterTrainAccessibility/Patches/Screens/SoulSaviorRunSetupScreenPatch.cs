using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    public static class SoulSaviorRunSetupScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("SoulSaviorRunSetupScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("SoulSaviorRunSetupScreen type not found (may not exist in this game version)");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(SoulSaviorRunSetupScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched SoulSaviorRunSetupScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("SoulSaviorRunSetupScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch SoulSaviorRunSetupScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.ClanSelection);
                MonsterTrainAccessibility.LogInfo("SoulSaviorRunSetupScreen screen entered");
                MonsterTrainAccessibility.ScreenReader?.Speak("Soul Savior Run Setup. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in SoulSaviorRunSetupScreenPatch patch: {ex.Message}");
            }
        }
    }
}
