using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for enhancer/upgrade card selection screen
    /// </summary>
    public static class EnhancerSelectionScreenPatch
    {
        private static string _lastEnhancerName = null;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                // Try different possible class names for the upgrade card selection screen
                var targetNames = new[] {
                    "UpgradeSelectionScreen",
                    "EnhancerSelectionScreen",
                    "CardUpgradeSelectionScreen",
                    "UpgradeScreen",
                    "EnhancerScreen"
                };

                foreach (var name in targetNames)
                {
                    var targetType = AccessTools.TypeByName(name);
                    if (targetType != null)
                    {
                        var method = AccessTools.Method(targetType, "Initialize") ??
                                     AccessTools.Method(targetType, "Setup") ??
                                     AccessTools.Method(targetType, "Show") ??
                                     AccessTools.Method(targetType, "Open");

                        if (method != null)
                        {
                            var postfix = new HarmonyMethod(typeof(EnhancerSelectionScreenPatch).GetMethod(nameof(Postfix)));
                            harmony.Patch(method, postfix: postfix);
                            MonsterTrainAccessibility.LogInfo($"Patched {name}.{method.Name}");
                            return;
                        }
                    }
                }

                MonsterTrainAccessibility.LogInfo("EnhancerSelectionScreen not found - will use alternative detection");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch EnhancerSelectionScreen: {ex.Message}");
            }
        }

        public static void SetEnhancerName(string name)
        {
            _lastEnhancerName = name;
        }

        public static void Postfix(object __instance)
        {
            try
            {
                // Try to get the card count from the screen
                int cardCount = 0;
                var instanceType = __instance.GetType();

                // Look for cards list or count
                var getCardsMethod = instanceType.GetMethod("GetCards", Type.EmptyTypes) ??
                                     instanceType.GetMethod("GetCardList", Type.EmptyTypes);
                if (getCardsMethod != null)
                {
                    var cards = getCardsMethod.Invoke(__instance, null) as IList;
                    if (cards != null)
                        cardCount = cards.Count;
                }

                // Also try cards field
                if (cardCount == 0)
                {
                    var fields = instanceType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var field in fields)
                    {
                        if (field.Name.ToLower().Contains("card"))
                        {
                            var value = field.GetValue(__instance);
                            if (value is IList list)
                            {
                                cardCount = list.Count;
                                break;
                            }
                        }
                    }
                }

                MonsterTrainAccessibility.DraftHandler?.OnEnhancerCardSelectionEntered(_lastEnhancerName, cardCount);
                _lastEnhancerName = null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in EnhancerSelectionScreen patch: {ex.Message}");
            }
        }
    }
}
