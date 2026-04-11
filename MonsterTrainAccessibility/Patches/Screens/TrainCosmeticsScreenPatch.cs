using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for Train Cosmetics screen (MT2)
    /// </summary>
    public static class TrainCosmeticsScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("TrainCosmeticsScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("TrainCosmeticsScreen type not found (may not exist in this game version)");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(TrainCosmeticsScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched TrainCosmeticsScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("TrainCosmeticsScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch TrainCosmeticsScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Train cosmetics screen entered");
                ScreenStateTracker.SetScreen(Help.GameScreen.TrainCosmetics);
                MonsterTrainAccessibility.ScreenReader?.Speak("Train Cosmetics. Customize your train appearance. Use arrow keys to browse options. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in TrainCosmeticsScreen patch: {ex.Message}");
            }
        }
    }
}
