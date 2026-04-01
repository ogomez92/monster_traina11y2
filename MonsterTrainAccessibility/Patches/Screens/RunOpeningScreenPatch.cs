using HarmonyLib;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for Run Opening screen (showing upcoming battles)
    /// </summary>
    public static class RunOpeningScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("RunOpeningScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("RunOpeningScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(RunOpeningScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched RunOpeningScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("RunOpeningScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RunOpeningScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Run opening screen entered");
                MonsterTrainAccessibility.ScreenReader?.Speak("Run Overview. Showing upcoming battles and challenges. Use arrow keys to browse. Press Enter to begin. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RunOpeningScreen patch: {ex.Message}");
            }
        }
    }
}
