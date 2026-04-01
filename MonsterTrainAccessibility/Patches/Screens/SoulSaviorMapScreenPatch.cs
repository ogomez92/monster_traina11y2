using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    public static class SoulSaviorMapScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("SoulSaviorMapScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("SoulSaviorMapScreen type not found (may not exist in this game version)");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(SoulSaviorMapScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched SoulSaviorMapScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("SoulSaviorMapScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch SoulSaviorMapScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.SoulSaviorMap);
                MonsterTrainAccessibility.LogInfo("SoulSaviorMapScreen screen entered");
                MonsterTrainAccessibility.ScreenReader?.Speak("Soul Savior Map. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in SoulSaviorMapScreenPatch patch: {ex.Message}");
            }
        }
    }
}
