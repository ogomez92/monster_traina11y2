using HarmonyLib;
using MonsterTrainAccessibility.Help;
using System;

namespace MonsterTrainAccessibility.Patches.Screens
{
    public static class EndlessMutatorDraftScreenPatch
    {
        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("EndlessMutatorDraftScreen");
                if (targetType == null)
                {
                    MonsterTrainAccessibility.LogInfo("EndlessMutatorDraftScreen type not found (may not exist in this game version)");
                    return;
                }

                var method = AccessTools.Method(targetType, "Initialize") ??
                             AccessTools.Method(targetType, "Setup") ??
                             AccessTools.Method(targetType, "Show");

                if (method != null)
                {
                    var postfix = new HarmonyMethod(typeof(EndlessMutatorDraftScreenPatch).GetMethod(nameof(Postfix)));
                    harmony.Patch(method, postfix: postfix);
                    MonsterTrainAccessibility.LogInfo($"Patched EndlessMutatorDraftScreen.{method.Name}");
                }
                else
                {
                    MonsterTrainAccessibility.LogInfo("EndlessMutatorDraftScreen methods not found");
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Failed to patch EndlessMutatorDraftScreen: {ex.Message}");
            }
        }

        public static void Postfix()
        {
            try
            {
                ScreenStateTracker.SetScreen(Help.GameScreen.MutatorDraft);
                MonsterTrainAccessibility.LogInfo("EndlessMutatorDraftScreen screen entered");
                MonsterTrainAccessibility.ScreenReader?.Speak("Mutator Selection. Choose mutators for your run.. Press F1 for help.");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error in EndlessMutatorDraftScreenPatch patch: {ex.Message}");
            }
        }
    }
}
