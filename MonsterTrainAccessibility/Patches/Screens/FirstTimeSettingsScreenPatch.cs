using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    public static class FirstTimeSettingsScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("FirstTimeSettingsScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("FirstTimeSettingsScreen type not found (may not exist in this game version)");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(FirstTimeSettingsScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched FirstTimeSettingsScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("FirstTimeSettingsScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch FirstTimeSettingsScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.Settings);
                MonsterTrainAccessibility.LogInfo("FirstTimeSettingsScreen screen entered");
                MonsterTrainAccessibility.ScreenReader?.Speak("First Time Settings. Configure your initial game settings.. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in FirstTimeSettingsScreenPatch patch: {ex.Message}");
            }
        }
    }
}
