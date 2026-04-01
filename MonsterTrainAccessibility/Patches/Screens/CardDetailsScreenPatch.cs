using HarmonyLib;
using MonsterTrainAccessibility.Utilities;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for Card Details popup
    /// </summary>
    public static class CardDetailsScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CardDetailsScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("CardDetailsScreen type not found");
                    return;
                }

                // First try ShowCard which has a CardState parameter
                var showCardMethod = AccessTools.Method(targetType, "ShowCard");
                if (showCardMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(CardDetailsScreenPatch).GetMethod(nameof(ShowCardPostfix)));
                    harmony.Patch(showCardMethod, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched CardDetailsScreen.ShowCard");
                    return;
                }

                // Fall back to Initialize/Setup
                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(CardDetailsScreenPatch).GetMethod(nameof(InitializePostfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched CardDetailsScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("CardDetailsScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CardDetailsScreen: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for ShowCard method which receives a CardState
        /// </summary>
        public static void ShowCardPostfix(object __instance, object __0)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("CardDetailsScreen.ShowCard called");

                // __0 is the CardState being shown
                if (__0 != null)
                {
                    string cardInfo = GetCardInfo(__0);
                    if (!string.IsNullOrEmpty(cardInfo))
                    {
                        MonsterTrainAccessibility.ScreenReader?.Speak($"Card Details: {cardInfo}. Press Escape to close.");
                        return;
                    }
                }

                MonsterTrainAccessibility.ScreenReader?.Speak("Card Details. Press Escape to close.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CardDetailsScreen.ShowCard patch: {ex.Message}");
                MonsterTrainAccessibility.ScreenReader?.Speak("Card Details. Press Escape to close.");
            }
        }

        /// <summary>
        /// Postfix for Initialize/Setup methods (no card parameter)
        /// </summary>
        public static void InitializePostfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("CardDetailsScreen initialized");
                MonsterTrainAccessibility.ScreenReader?.Speak("Card Details. Press Escape to close.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CardDetailsScreen.Initialize patch: {ex.Message}");
            }
        }

        private static string GetCardInfo(object cardState)
        {
            try
            {
                if (cardState == null) return null;

                var cardType = cardState.GetType();
                MonsterTrainAccessibility.LogInfo($"Getting card info from type: {cardType.Name}");

                // Get card name
                var getTitleMethod = cardType.GetMethod("GetTitle") ?? cardType.GetMethod("GetName");
                string name = getTitleMethod?.Invoke(cardState, null) as string ?? "Unknown";

                // Get cost
                var getCostMethod = cardType.GetMethod("GetCost") ?? cardType.GetMethod("GetCostWithoutAnyModifications");
                int cost = 0;
                if (getCostMethod != null)
                {
                    var costResult = getCostMethod.Invoke(cardState, null);
                    if (costResult is int c) cost = c;
                }

                // Get description
                var getDescMethod = cardType.GetMethod("GetDescription");
                string desc = getDescMethod?.Invoke(cardState, null) as string ?? "";
                desc = TextUtilities.StripRichTextTags(desc);

                MonsterTrainAccessibility.LogInfo($"Card info: {name}, {cost} ember");
                return $"{name}, {cost} ember. {desc}";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting card info: {ex.Message}");
            }
            return null;
        }
    }
}
