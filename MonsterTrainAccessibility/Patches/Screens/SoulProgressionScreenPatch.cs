using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    public static class SoulProgressionScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("SoulProgressionScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("SoulProgressionScreen type not found (may not exist in this game version)");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(SoulProgressionScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched SoulProgressionScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("SoulProgressionScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch SoulProgressionScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.SoulProgression);
                MonsterTrainAccessibility.LogInfo("SoulProgressionScreen screen entered");
                MonsterTrainAccessibility.ScreenReader?.Speak("Soul Progression. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in SoulProgressionScreenPatch patch: {ex.Message}");
            }
        }
    }
}
