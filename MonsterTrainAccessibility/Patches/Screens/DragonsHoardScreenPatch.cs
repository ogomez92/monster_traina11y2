using HarmonyLib;
using MonsterTrainAccessibility.Help;
using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for Dragon's Hoard screen (MT2 specific)
    /// </summary>
    public static class DragonsHoardScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("DragonsHoardScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("DragonsHoardScreen type not found (may not exist in this game version)");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(DragonsHoardScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched DragonsHoardScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("DragonsHoardScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch DragonsHoardScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Dragon's Hoard screen entered");
                ScreenStateTracker.SetScreen(Help.GameScreen.DragonsHoard);

                var (amount, cap) = GetHoardCounts(__instance);
                string hoardName = Utilities.ModLocalization.DragonsHoard;
                string countText = cap > 0
                    ? $" {amount}/{cap} {hoardName} stored."
                    : amount > 0 ? $" {amount} {hoardName} stored." : "";

                var rewardTitles = GetCurrentRewardTitles(__instance);
                string rewardsText;
                if (rewardTitles.Count > 0)
                {
                    rewardsText = $" Loot Level {amount} rewards: {string.Join(", ", rewardTitles)}.";
                }
                else if (amount <= 0)
                {
                    rewardsText = $" No {hoardName} stored. Earn {hoardName} to unlock rewards.";
                }
                else
                {
                    rewardsText = "";
                }

                string buttonHint = amount > 0
                    ? " Confirm to collect, Loot Levels to preview tiers, Cancel to leave."
                    : " Loot Levels to preview tiers, Cancel to leave.";

                MonsterTrainAccessibility.ScreenReader?.Speak($"{hoardName}.{countText}{rewardsText}{buttonHint} Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in DragonsHoardScreen patch: {ex.Message}");
            }
        }

        private static List<string> GetCurrentRewardTitles(object screen)
        {
            var titles = new List<string>();
            if (screen == null) return titles;
            try
            {
                var field = screen.GetType().GetField("_dragonsHoardRewardStates",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (!(field?.GetValue(screen) is IEnumerable rewardStates)) return titles;

                foreach (var rs in rewardStates)
                {
                    if (rs == null) continue;
                    var rdProp = rs.GetType().GetProperty("RewardData",
                        BindingFlags.Public | BindingFlags.Instance);
                    var rd = rdProp?.GetValue(rs);
                    if (rd == null) continue;
                    var titleProp = rd.GetType().GetProperty("RewardTitle",
                        BindingFlags.Public | BindingFlags.Instance);
                    var title = titleProp?.GetValue(rd) as string;
                    if (string.IsNullOrEmpty(title)) continue;
                    title = TextUtilities.CleanSpriteTagsForSpeech(title);
                    if (!string.IsNullOrEmpty(title)) titles.Add(title);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"GetCurrentRewardTitles error: {ex.Message}");
            }
            return titles;
        }

        private static (int amount, int cap) GetHoardCounts(object screen)
        {
            object saveManager = null;
            try
            {
                if (screen != null)
                {
                    var field = screen.GetType().GetField("saveManager",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    saveManager = field?.GetValue(screen);
                }
            }
            catch { }

            if (saveManager == null)
            {
                saveManager = Utilities.ReflectionHelper.FindManager("SaveManager");
            }

            if (saveManager == null) return (0, 0);

            int amount = 0, cap = 0;
            try
            {
                var t = saveManager.GetType();
                var getAmt = t.GetMethod("GetDragonsHoardAmount", Type.EmptyTypes);
                var getCap = t.GetMethod("GetDragonsHoardCap", Type.EmptyTypes);
                if (getAmt?.Invoke(saveManager, null) is int a) amount = a;
                if (getCap?.Invoke(saveManager, null) is int c) cap = c;
            }
            catch { }
            return (amount, cap);
        }
    }
}
