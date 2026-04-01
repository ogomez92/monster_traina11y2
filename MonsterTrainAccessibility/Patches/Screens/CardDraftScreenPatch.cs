using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Detect card draft screen
    /// </summary>
    public static class CardDraftScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CardDraftScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Setup");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(CardDraftScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched CardDraftScreen.Setup");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CardDraftScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.CardDraft);
                // Extract draft cards from __instance and call handler
                // This would parse the actual CardDraftScreen to get card data
                MonsterTrainAccessibility.LogInfo("Card draft screen detected");

                // For now, announce generic draft entry
                MonsterTrainAccessibility.ScreenReader?.AnnounceScreen("Card Draft. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CardDraftScreen patch: {ex.Message}");
            }
        }
    }
}
