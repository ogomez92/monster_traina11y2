using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    /// <summary>
    /// Patch for Elixir draft screen (MT2 specific)
    /// </summary>
    public static class ElixirDraftScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("ElixirDraftScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("ElixirDraftScreen type not found (may not exist in this game version)");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(ElixirDraftScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched ElixirDraftScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("ElixirDraftScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch ElixirDraftScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                MonsterTrainAccessibility.LogInfo("Elixir draft screen entered");
                ScreenStateTracker.SetScreen(Help.GameScreen.ElixirDraft);
                MonsterTrainAccessibility.ScreenReader?.Speak("Elixir Selection. Choose an elixir to modify your run. Use arrow keys to browse, Enter to select. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in ElixirDraftScreen patch: {ex.Message}");
            }
        }
    }
}
