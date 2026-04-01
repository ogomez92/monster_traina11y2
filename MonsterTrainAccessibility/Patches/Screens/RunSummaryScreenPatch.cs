using HarmonyLib;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for Run Summary screen (end of run stats)
    /// </summary>
    public static class RunSummaryScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("RunSummaryScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("RunSummaryScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(RunSummaryScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched RunSummaryScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("RunSummaryScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RunSummaryScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Run summary screen entered");
                // The GameOverScreenPatch may already handle this, but this is a backup
                MonsterTrainAccessibility.ScreenReader?.Speak("Run Summary. Press T to read stats. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RunSummaryScreen patch: {ex.Message}");
            }
        }
    }
}
