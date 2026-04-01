using HarmonyLib;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Announce win streak when it appears on the game over screen.
    /// </summary>
    public static class WinStreakPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // WinStreakIncreaseUI extends WinStreakUI. The Set() method is called
                // when the win streak display is updated. No AnimateInCoroutine exists.
                var targetType = AccessTools.TypeByName("WinStreakIncreaseUI");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("WinStreakIncreaseUI not found");
                    return;
                }

                // Set(int winStreak, int lowestAscensionLevel, bool trueFinalBossStreak) is on WinStreakIncreaseUI (override)
                var setMethod = AccessTools.Method(targetType, "Set");
                if (setMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(WinStreakPatch).GetMethod(nameof(SetPostfix)));
                    harmony.Patch(setMethod, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo("Patched WinStreakIncreaseUI.Set");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("WinStreakIncreaseUI.Set not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch WinStreakIncreaseUI: {ex.Message}");
            }
        }

        // Set(int winStreak, int lowestAscensionLevel, bool trueFinalBossStreak)
        // __0 = winStreak
        public static void SetPostfix(object __instance, int __0)
        {
            try
            {
                if (__instance == null || __0 <= 0) return;

                int winStreak = __0;
                int previousStreak = winStreak - 1;

                string announcement;
                if (previousStreak > 0)
                {
                    announcement = $"Win streak increased! {previousStreak} to {winStreak}";
                }
                else
                {
                    announcement = $"Win streak: {winStreak}";
                }
                MonsterTrainAccessibility.LogInfo($"Win streak: {announcement}");
                MonsterTrainAccessibility.ScreenReader?.Queue(announcement);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in WinStreakPatch: {ex.Message}");
            }
        }
    }
}
