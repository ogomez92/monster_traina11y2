using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Detect when main menu is shown
    /// </summary>
    public static class MainMenuScreenPatch
    {
        // Target: MainMenuScreen.Initialize
        // This will be resolved at runtime when the game DLLs are available

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("MainMenuScreen");
                if (targetType != null)
                {
                    var method = AccessTools.Method(targetType, "Initialize");
                    if (method != null)
                    {
                        var postfix = new HarmonyMethod(typeof(MainMenuScreenPatch).GetMethod(nameof(Postfix)));
                        harmony.Patch(method, postfix: postfix);
                        MonsterTrainAccessibility.LogInfo("Patched MainMenuScreen.Initialize");
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch MainMenuScreen: {ex.Message}");
            }
        }

        public static void Postfix(object __instance)
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("MainMenuScreen.Initialize called!");
                ScreenStateTracker.SetScreen(Help.GameScreen.MainMenu);

                if (MonsterTrainAccessibility.MenuHandler == null)
                {
                    MonsterTrainAccessibility.LogInfo("MenuHandler is null - announcing directly");
                    MonsterTrainAccessibility.ScreenReader?.Speak("Main Menu. Press F1 for help.", false);
                }
                else
                {
                    MonsterTrainAccessibility.MenuHandler.OnMainMenuEntered(__instance);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in MainMenuScreen patch: {ex.Message}");
            }
        }
    }
}
