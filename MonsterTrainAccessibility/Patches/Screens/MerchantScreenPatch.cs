using HarmonyLib;
using MonsterTrainAccessibility.Core;
using MonsterTrainAccessibility.Help;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for merchant/shop screen
    /// </summary>
    public static class MerchantScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("MerchantScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize") ??
                                 AccessTools.Method(targetType, "Setup") ??
                                 AccessTools.Method(targetType, "Open") ??
                                 AccessTools.Method(targetType, "Show");

                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(MerchantScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo($"Patched MerchantScreen.{method.Name}");
                    }
                    else
                    {
                        MonsterTrainAccessibility.LogInfo("MerchantScreen methods not found");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch MerchantScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Shop);

                // Announce gold when entering shop
                int gold = InputInterceptor.GetCurrentGold();
                string goldLabel = Utilities.ModLocalization.Gold;
                string goldText = gold >= 0 ? $"{gold} {goldLabel}." : "";

                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen($"Shop. {goldText} Press F1 for help");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in MerchantScreen patch: {ex.Message}");
            }
        }
    }
}
