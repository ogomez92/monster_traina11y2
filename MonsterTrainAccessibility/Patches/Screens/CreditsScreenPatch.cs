using HarmonyLib;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for credits screen
    /// </summary>
    public static class CreditsScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CreditsScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("CreditsScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(CreditsScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched CreditsScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("CreditsScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CreditsScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Credits screen entered");
                MonsterTrainAccessibility.ScreenReader?.Speak("Credits. Press Escape to return to main menu.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CreditsScreen patch: {ex.Message}");
            }
        }
    }
}
