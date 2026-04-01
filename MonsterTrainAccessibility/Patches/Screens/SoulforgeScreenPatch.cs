using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    public static class SoulforgeScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("SoulforgeScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("SoulforgeScreen type not found (may not exist in this game version)");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(SoulforgeScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched SoulforgeScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("SoulforgeScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch SoulforgeScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Soulforge);
                MonsterTrainAccessibility.LogInfo("SoulforgeScreen screen entered");
                MonsterTrainAccessibility.ScreenReader?.Speak("Soulforge. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in SoulforgeScreenPatch patch: {ex.Message}");
            }
        }
    }
}
