using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    public static class RegionSelectionScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("RegionSelectionScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("RegionSelectionScreen type not found (may not exist in this game version)");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(RegionSelectionScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched RegionSelectionScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("RegionSelectionScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RegionSelectionScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.RegionSelection);
                MonsterTrainAccessibility.LogInfo("RegionSelectionScreen screen entered");
                MonsterTrainAccessibility.ScreenReader?.Speak("Region Selection. Choose a region.. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RegionSelectionScreenPatch patch: {ex.Message}");
            }
        }
    }
}
