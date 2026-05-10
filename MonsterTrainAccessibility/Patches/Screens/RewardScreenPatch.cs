using HarmonyLib;
using MonsterTrainAccessibility.Help;
using MonsterTrainAccessibility.Utilities;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for reward screen (after battle rewards)
    /// </summary>
    public static class RewardScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("RewardScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("RewardScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(RewardScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched RewardScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("RewardScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch RewardScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Rewards);
                MonsterTrainAccessibility.LogInfo("Reward screen entered");

                MonsterTrainAccessibility.ScreenReader?.Speak(ModLocalization.ModTerm("RewardsIntro"));
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RewardScreen patch: {ex.Message}");
            }
        }
    }
}
