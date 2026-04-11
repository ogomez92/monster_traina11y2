using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;
using System.Reflection;

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

                // Try to get gold count
                int gold = GetHoardGold(__instance);
                string goldText = gold > 0 ? $" {gold} gold stored." : "";

                MonsterTrainAccessibility.ScreenReader?.Speak($"Dragon's Hoard.{goldText} Use arrow keys to browse options. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in DragonsHoardScreen patch: {ex.Message}");
            }
        }

        private static int GetHoardGold(object screen)
        {
            try
            {
                if (screen == null) return 0;
                var screenType = screen.GetType();

                // Look for gold/hoard value
                var fields = screenType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("gold") || fieldName.Contains("hoard") || fieldName.Contains("value"))
                    {
                        var value = field.GetValue(screen);
                        if (value is int g)
                        {
                            return g;
                        }
                    }
                }
            }
            catch { }
            return 0;
        }
    }
}
