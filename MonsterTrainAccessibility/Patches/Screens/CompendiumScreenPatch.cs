using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for compendium/logbook screen
    /// </summary>
    public static class CompendiumScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("CompendiumScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("CompendiumScreen type not found");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(CompendiumScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched CompendiumScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("CompendiumScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch CompendiumScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Compendium screen entered");
                ScreenStateTracker.SetScreen(Help.GameScreen.Compendium);
                MonsterTrainAccessibility.ScreenReader?.Speak("Compendium. Browse cards, clans, and game information. Use Tab to switch categories. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in CompendiumScreen patch: {ex.Message}");
            }
        }
    }
}
