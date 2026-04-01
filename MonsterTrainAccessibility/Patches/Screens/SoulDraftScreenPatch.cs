using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    public static class SoulDraftScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("SoulDraftScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("SoulDraftScreen type not found (may not exist in this game version)");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(SoulDraftScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched SoulDraftScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("SoulDraftScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch SoulDraftScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.SoulDraft);
                MonsterTrainAccessibility.LogInfo("SoulDraftScreen screen entered");
                MonsterTrainAccessibility.ScreenReader?.Speak("Soul Draft. Choose a soul.. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in SoulDraftScreenPatch patch: {ex.Message}");
            }
        }
    }
}
