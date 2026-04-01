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

                // Count rewards if possible
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

                // Look for rewards list
                var fields = screenType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (field.Name.ToLower().Contains("reward"))
                    {
                        var value = field.GetValue(screen);
                        if (value is IList list)
                        {
                            return list.Count;
                        }
                    }
                }
            }
            catch { }
            return 0;
        }
    }
}
