using HarmonyLib;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for Minimap screen
    /// </summary>
    public static class MinimapScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("MinimapScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("MinimapScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(MinimapScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched MinimapScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("MinimapScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch MinimapScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Minimap screen entered");
                MonsterTrainAccessibility.ScreenReader?.Speak("Minimap. View run progress and upcoming nodes. Press Escape to close.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in MinimapScreen patch: {ex.Message}");
            }
        }
    }
}
