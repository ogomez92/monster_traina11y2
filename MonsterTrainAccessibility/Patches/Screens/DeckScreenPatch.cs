using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for deck screen (viewing your deck)
    /// </summary>
    public static class DeckScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("DeckScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("DeckScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(DeckScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched DeckScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("DeckScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch DeckScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Deck screen entered");
                Help.Contexts.DeckViewHelp.SetActive(true);

                // Count cards if possible
                int cardCount = CountCards(__instance);
                string countText = cardCount > 0 ? $" Your deck has {cardCount} cards." : "";

                MonsterTrainAccessibility.ScreenReader?.Speak($"Deck View.{countText} Use arrow keys to browse cards. Press Escape to close. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in DeckScreen patch: {ex.Message}");
            }
        }

        private static int CountCards(object screen)
        {
            try
            {
                if (screen == null) return 0;
                var screenType = screen.GetType();

                var fields = screenType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("card") && (fieldName.Contains("list") || fieldName.Contains("deck")))
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
