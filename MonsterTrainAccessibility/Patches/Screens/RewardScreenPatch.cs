using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;
using System.Collections;
using System.Reflection;

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

                // Count rewards from pendingRewards (the actual reward list, not UI slots)
                int rewardCount = CountRewards(__instance);
                string countText = rewardCount > 0 ? $" {rewardCount} rewards available." : "";

                MonsterTrainAccessibility.ScreenReader?.Speak($"Rewards.{countText} Use arrow keys to browse, Enter to select. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in RewardScreen patch: {ex.Message}");
            }
        }

        private static int CountRewards(object screen)
        {
            try
            {
                if (screen == null) return 0;
                var screenType = screen.GetType();

                // Use pendingRewards specifically - this is the list of actual rewards
                // to show, not the UI slots (rewardDetailsUIs) or display list (currentRewards).
                var pendingField = screenType.GetField("pendingRewards",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (pendingField != null)
                {
                    var value = pendingField.GetValue(screen);
                    if (value is IList list)
                        return list.Count;
                }
            }
            catch { }
            return 0;
        }
    }
}
